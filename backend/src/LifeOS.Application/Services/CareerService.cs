using LifeOS.Application.Ai;
using LifeOS.Application.Common;
using LifeOS.Application.DTO.Ai;
using LifeOS.Application.DTO.Career;
using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Application.Interfaces.Repositories;
using LifeOS.Application.Interfaces.Services;
using LifeOS.Domain.Entities;
using LifeOS.Domain.Enums;
using LifeOS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Application.Services;

/// <summary>
/// Карьерный модуль: профиль и AI-разбор резюме.
/// Профиль связан с пользователем 1:1 и создаётся лениво — при первом обращении.
/// </summary>
public class CareerService : ICareerService
{
    private const int MinResumeLength = 50;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _storage;
    private readonly IDocumentTextExtractor _textExtractor;
    private readonly IAiService _ai;
    private readonly IAiHistoryRecorder _history;

    public CareerService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IFileStorageService storage,
        IDocumentTextExtractor textExtractor,
        IAiService ai,
        IAiHistoryRecorder history)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _storage = storage;
        _textExtractor = textExtractor;
        _ai = ai;
        _history = history;
    }

    public async Task<CareerProfileResponse> GetProfileAsync(CancellationToken cancellationToken = default)
        => ToResponse(await LoadOrCreateProfileAsync(cancellationToken));

    public async Task<CareerProfileResponse> UpdateProfileAsync(
        UpdateCareerProfileRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();
        var profile = await LoadOrCreateProfileAsync(cancellationToken);

        if (request.ResumeFileId.HasValue)
        {
            var file = await _unitOfWork.Files.Query()
                .FirstOrDefaultAsync(f => f.Id == request.ResumeFileId.Value, cancellationToken);

            var owned = CrudGuard.EnsureOwned(
                file, file?.UserId ?? Guid.Empty, userId, nameof(StoredFile), request.ResumeFileId.Value);

            if (owned.ContentType != "application/pdf")
                throw new BusinessRuleException("Резюме должно быть PDF-файлом.", "career.pdf_required");
        }

        profile.Skills = request.Skills?.Trim();
        profile.DesiredPosition = request.DesiredPosition?.Trim();
        profile.ResumeFileId = request.ResumeFileId;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetProfileAsync(cancellationToken);
    }

    public async Task<AiResultResponse<ResumeAnalysisResponse>> AnalyzeResumeAsync(
        CancellationToken cancellationToken = default)
    {
        var profile = await LoadOrCreateProfileAsync(cancellationToken);

        if (!profile.ResumeFileId.HasValue)
            throw new BusinessRuleException(
                "Резюме не загружено. Сначала загрузите PDF и привяжите его к профилю.",
                "career.resume_missing");

        var file = await _unitOfWork.Files.GetByIdAsync(profile.ResumeFileId.Value, cancellationToken)
            ?? throw new NotFoundException(nameof(StoredFile), profile.ResumeFileId.Value);

        await using var content = await _storage.DownloadAsync(file.StoragePath, cancellationToken);
        var text = await _textExtractor.ExtractTextAsync(content, cancellationToken);

        if (text.Length < MinResumeLength)
            throw new BusinessRuleException(
                "Не удалось извлечь текст из резюме. Вероятно, это скан без текстового слоя.",
                "career.no_text_layer");

        var skills = (profile.Skills ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var envelope = await _ai.AnalyzeResumeAsync(
            new AiContracts.ResumeAnalysisRequest(text, profile.DesiredPosition, skills, "ru"),
            cancellationToken);

        var analysis = envelope.Result;

        // Разбор сохраняется в профиль: пользователь должен видеть его
        // и после перезагрузки страницы, не запуская анализ заново.
        profile.AiReview = BuildReviewText(analysis);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _history.RecordAsync(
            "/career/resume-analysis",
            new { resumeLength = text.Length, profile.DesiredPosition },
            new { analysis.OverallScore, analysis.Source },
            envelope.Confidence,
            envelope.IsConfident,
            ModuleType.Career,
            recommendationText: BuildRecommendation(analysis),
            cancellationToken);

        var response = new ResumeAnalysisResponse(
            analysis.OverallScore,
            analysis.Strengths,
            analysis.Weaknesses,
            analysis.MissingSkills,
            analysis.Suggestions,
            analysis.Source);

        return envelope.ToResponse(response);
    }

    // ---- Вспомогательные методы ------------------------------------------

    /// <summary>
    /// Профиль создаётся при первом обращении, а не при регистрации:
    /// иначе у каждого пользователя висела бы пустая запись, даже если
    /// карьерным модулем он никогда не пользовался.
    /// </summary>
    private async Task<CareerProfile> LoadOrCreateProfileAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var profile = await _unitOfWork.CareerProfiles.Query(asNoTracking: false)
            .Include(c => c.ResumeFile)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (profile is not null)
            return profile;

        profile = new CareerProfile { UserId = userId };

        await _unitOfWork.CareerProfiles.AddAsync(profile, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return profile;
    }

    private static CareerProfileResponse ToResponse(CareerProfile profile)
        => new(
            profile.Id,
            profile.ResumeFileId,
            profile.ResumeFile?.FileName,
            profile.Skills,
            profile.DesiredPosition,
            profile.AiReview,
            profile.UpdatedAt);

    private static string BuildReviewText(AiContracts.ResumeAnalysis analysis)
    {
        var parts = new List<string> { $"Оценка резюме: {analysis.OverallScore:0}/100." };

        if (analysis.Strengths.Count > 0)
            parts.Add("Сильные стороны: " + string.Join("; ", analysis.Strengths) + ".");

        if (analysis.Weaknesses.Count > 0)
            parts.Add("Слабые места: " + string.Join("; ", analysis.Weaknesses) + ".");

        if (analysis.MissingSkills.Count > 0)
            parts.Add("Не хватает навыков: " + string.Join(", ", analysis.MissingSkills) + ".");

        if (analysis.Suggestions.Count > 0)
            parts.Add("Что улучшить: " + string.Join("; ", analysis.Suggestions) + ".");

        return string.Join(" ", parts);
    }

    private static string? BuildRecommendation(AiContracts.ResumeAnalysis analysis)
    {
        // В ленту рекомендаций попадает самое действенное — первое предложение
        // по улучшению. Вываливать туда весь разбор целиком бессмысленно.
        if (analysis.Suggestions.Count > 0)
            return analysis.Suggestions[0];

        return analysis.MissingSkills.Count > 0
            ? $"Стоит освоить навыки, которых не хватает в резюме: {string.Join(", ", analysis.MissingSkills.Take(3))}."
            : null;
    }
}
