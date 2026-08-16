using System.Text.Json;
using AutoMapper;
using LifeOS.Application.Ai;
using LifeOS.Application.Common;
using LifeOS.Application.DTO.Ai;
using LifeOS.Application.DTO.Study;
using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Application.Interfaces.Repositories;
using LifeOS.Application.Interfaces.Services;
using LifeOS.Domain.Entities;
using LifeOS.Domain.Enums;
using LifeOS.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LifeOS.Application.Services;

/// <summary>
/// Учебный модуль: материалы, конспекты, тесты, заметки.
///
/// Схема работы: файл уже загружен через /api/files/upload (там же прошёл
/// валидацию), здесь он привязывается к материалу, из него извлекается
/// текст и отправляется в AI-сервис.
/// </summary>
public class StudyService : IStudyService
{
    private const int MinTextLength = 50;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService _storage;
    private readonly IDocumentTextExtractor _textExtractor;
    private readonly IAiService _ai;
    private readonly IAiHistoryRecorder _history;
    private readonly IMapper _mapper;
    private readonly ILogger<StudyService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public StudyService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IFileStorageService storage,
        IDocumentTextExtractor textExtractor,
        IAiService ai,
        IAiHistoryRecorder history,
        IMapper mapper,
        ILogger<StudyService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _storage = storage;
        _textExtractor = textExtractor;
        _ai = ai;
        _history = history;
        _mapper = mapper;
        _logger = logger;
    }

    // ---- Материалы -------------------------------------------------------

    public async Task<PagedResult<StudyMaterialResponse>> GetMaterialsAsync(
        PaginationParams pagination, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var source = _unitOfWork.StudyMaterials.Query().Where(m => m.UserId == userId);

        var totalCount = await source.CountAsync(cancellationToken);

        var items = await source
            .Include(m => m.File)
            .Include(m => m.Notes)
            .Include(m => m.Quizzes)
            .OrderByDescending(m => m.CreatedAt)
            .Skip(pagination.Skip)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StudyMaterial>(items, totalCount, pagination.PageNumber, pagination.PageSize)
            .Map(_mapper.Map<StudyMaterialResponse>);
    }

    public async Task<StudyMaterialResponse> GetMaterialAsync(
        Guid id, CancellationToken cancellationToken = default)
        => _mapper.Map<StudyMaterialResponse>(await LoadMaterialWithDetailsAsync(id, cancellationToken));

    public async Task<StudyMaterialResponse> CreateMaterialAsync(
        CreateStudyMaterialRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var file = await _unitOfWork.Files.Query()
            .FirstOrDefaultAsync(f => f.Id == request.FileId, cancellationToken);

        var ownedFile = CrudGuard.EnsureOwned(
            file, file?.UserId ?? Guid.Empty, userId, nameof(StoredFile), request.FileId);

        if (ownedFile.ContentType != "application/pdf")
            throw new BusinessRuleException(
                "Учебный материал создаётся только из PDF-файла.", "study.pdf_required");

        // Один файл — один материал: иначе один PDF порождал бы несколько
        // конспектов, а удаление файла блокировалось бы неочевидным образом.
        var alreadyUsed = await _unitOfWork.StudyMaterials.AnyAsync(
            m => m.FileId == request.FileId, cancellationToken);

        if (alreadyUsed)
            throw new ConflictException(
                "На основе этого файла уже создан учебный материал.", "study.file_already_used");

        var material = new StudyMaterial
        {
            UserId = userId,
            FileId = request.FileId,
            Title = request.Title.Trim()
        };

        await _unitOfWork.StudyMaterials.AddAsync(material, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return await GetMaterialAsync(material.Id, cancellationToken);
    }

    public async Task DeleteMaterialAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var material = await LoadMaterialTrackedAsync(id, cancellationToken);

        // Заметки и тесты уходят каскадом (правила из Фазы 1).
        // Сам файл остаётся: он принадлежит пользователю, а не материалу,
        // и удаляется отдельно через модуль Files.
        _unitOfWork.StudyMaterials.Remove(material);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ---- AI: конспект ----------------------------------------------------

    public async Task<AiResultResponse<StudySummaryResult>> SummarizeAsync(
        Guid materialId, CancellationToken cancellationToken = default)
    {
        var material = await LoadMaterialTrackedAsync(materialId, cancellationToken);
        var text = await ExtractTextAsync(material, cancellationToken);

        var request = new AiContracts.StudySummaryRequest(text, MaxSentences: 7, Language: "ru");
        var envelope = await _ai.SummarizeAsync(request, cancellationToken);

        material.Summary = envelope.Result.Summary;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // В аудит пишем длину текста, а не сам текст: полное содержимое
        // учебника в таблице истории — это и объём, и лишние персональные данные.
        await _history.RecordAsync(
            "/study/summary",
            new { materialId, textLength = text.Length },
            envelope.Result,
            envelope.Confidence,
            envelope.IsConfident,
            ModuleType.Study,
            recommendationText: null,
            cancellationToken);

        var result = new StudySummaryResult(
            envelope.Result.Summary,
            envelope.Result.KeyPoints ?? new List<string>(),
            envelope.Result.Source);

        return envelope.ToResponse(result);
    }

    // ---- AI: тесты -------------------------------------------------------

    public async Task<AiResultResponse<QuizResponse>> GenerateQuizAsync(
        GenerateQuizRequest request, CancellationToken cancellationToken = default)
    {
        var material = await LoadMaterialTrackedAsync(request.StudyMaterialId, cancellationToken);
        var text = await ExtractTextAsync(material, cancellationToken);

        var envelope = await _ai.GenerateQuizAsync(
            new AiContracts.QuizRequest(text, request.QuestionCount, "ru"), cancellationToken);

        if (envelope.Result.Questions.Count == 0)
            throw new BusinessRuleException(
                "AI-сервис не смог составить тест по этому материалу. " +
                "Возможно, не настроен ключ внешней языковой модели.",
                "study.quiz_unavailable");

        var quiz = new Quiz
        {
            UserId = material.UserId,
            StudyMaterialId = material.Id,
            // Правильные ответы хранятся в БД, но НЕ отдаются клиенту
            // до проверки — иначе тест решался бы через инструменты разработчика.
            Questions = JsonSerializer.Serialize(envelope.Result.Questions, JsonOptions),
            TotalQuestions = envelope.Result.Questions.Count
        };

        await _unitOfWork.Quizzes.AddAsync(quiz, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _history.RecordAsync(
            "/study/quiz",
            new { request.StudyMaterialId, request.QuestionCount },
            new { questionCount = quiz.TotalQuestions },
            envelope.Confidence,
            envelope.IsConfident,
            ModuleType.Study,
            recommendationText: null,
            cancellationToken);

        return envelope.ToResponse(ToQuizResponse(quiz));
    }

    public async Task<QuizResponse> GetQuizAsync(Guid quizId, CancellationToken cancellationToken = default)
        => ToQuizResponse(await LoadQuizAsync(quizId, tracked: false, cancellationToken));

    public async Task<QuizGradeResponse> SubmitQuizAsync(
        Guid quizId, SubmitQuizRequest request, CancellationToken cancellationToken = default)
    {
        var quiz = await LoadQuizAsync(quizId, tracked: true, cancellationToken);
        var questions = DeserializeQuestions(quiz);

        if (request.Answers.Count != questions.Count)
            throw new BusinessRuleException(
                $"Ожидается {questions.Count} ответов, получено {request.Answers.Count}.",
                "study.answers_count_mismatch");

        var results = new List<QuizAnswerResult>();
        var score = 0;

        for (var i = 0; i < questions.Count; i++)
        {
            var submitted = request.Answers[i];
            var isCorrect = submitted == questions[i].CorrectIndex;

            if (isCorrect) score++;

            results.Add(new QuizAnswerResult(
                i, submitted, questions[i].CorrectIndex, isCorrect, questions[i].Explanation));
        }

        quiz.Score = score;
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new QuizGradeResponse(quiz.Id, score, questions.Count, results);
    }

    // ---- Заметки ---------------------------------------------------------

    public async Task<IReadOnlyList<StudyNoteResponse>> GetNotesAsync(
        Guid materialId, CancellationToken cancellationToken = default)
    {
        // Проверяем владение материалом — иначе через его Id можно было бы
        // прочитать чужие заметки.
        await LoadMaterialTrackedAsync(materialId, cancellationToken);

        var notes = await _unitOfWork.StudyNotes.Query()
            .Where(n => n.StudyMaterialId == materialId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);

        return notes.Select(_mapper.Map<StudyNoteResponse>).ToList();
    }

    public async Task<StudyNoteResponse> CreateNoteAsync(
        CreateStudyNoteRequest request, CancellationToken cancellationToken = default)
    {
        var material = await LoadMaterialTrackedAsync(request.StudyMaterialId, cancellationToken);

        var note = new StudyNote
        {
            UserId = material.UserId,
            StudyMaterialId = material.Id,
            Content = request.Content.Trim()
        };

        await _unitOfWork.StudyNotes.AddAsync(note, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<StudyNoteResponse>(note);
    }

    public async Task<StudyNoteResponse> UpdateNoteAsync(
        Guid id, UpdateStudyNoteRequest request, CancellationToken cancellationToken = default)
    {
        var note = await LoadNoteAsync(id, cancellationToken);

        note.Content = request.Content.Trim();
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<StudyNoteResponse>(note);
    }

    public async Task DeleteNoteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var note = await LoadNoteAsync(id, cancellationToken);

        _unitOfWork.StudyNotes.Remove(note);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    // ---- Вспомогательные методы ------------------------------------------

    private async Task<string> ExtractTextAsync(StudyMaterial material, CancellationToken cancellationToken)
    {
        var file = material.File
            ?? await _unitOfWork.Files.GetByIdAsync(material.FileId, cancellationToken)
            ?? throw new NotFoundException(nameof(StoredFile), material.FileId);

        await using var content = await _storage.DownloadAsync(file.StoragePath, cancellationToken);

        var text = await _textExtractor.ExtractTextAsync(content, cancellationToken);

        if (text.Length < MinTextLength)
        {
            // Скан без текстового слоя — распространённый случай.
            // Пользователю нужно понятное объяснение, а не пустой конспект.
            _logger.LogWarning(
                "Из материала {MaterialId} извлечено {Length} символов — недостаточно.",
                material.Id, text.Length);

            throw new BusinessRuleException(
                "Не удалось извлечь текст из документа. Вероятно, это скан без текстового слоя — " +
                "для таких файлов нужен OCR, который в проекте не используется.",
                "study.no_text_layer");
        }

        return text;
    }

    private async Task<StudyMaterial> LoadMaterialTrackedAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var material = await _unitOfWork.StudyMaterials.Query(asNoTracking: false)
            .Include(m => m.File)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        return CrudGuard.EnsureOwned(
            material, material?.UserId ?? Guid.Empty, userId, nameof(StudyMaterial), id);
    }

    private async Task<StudyMaterial> LoadMaterialWithDetailsAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var material = await _unitOfWork.StudyMaterials.Query()
            .Include(m => m.File)
            .Include(m => m.Notes)
            .Include(m => m.Quizzes)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

        return CrudGuard.EnsureOwned(
            material, material?.UserId ?? Guid.Empty, userId, nameof(StudyMaterial), id);
    }

    private async Task<Quiz> LoadQuizAsync(Guid id, bool tracked, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var quiz = await _unitOfWork.Quizzes.Query(asNoTracking: !tracked)
            .FirstOrDefaultAsync(q => q.Id == id, cancellationToken);

        return CrudGuard.EnsureOwned(quiz, quiz?.UserId ?? Guid.Empty, userId, nameof(Quiz), id);
    }

    private async Task<StudyNote> LoadNoteAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetRequiredUserId();

        var note = await _unitOfWork.StudyNotes.Query(asNoTracking: false)
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

        return CrudGuard.EnsureOwned(note, note?.UserId ?? Guid.Empty, userId, nameof(StudyNote), id);
    }

    private static List<AiContracts.QuizQuestion> DeserializeQuestions(Quiz quiz)
    {
        var questions = JsonSerializer.Deserialize<List<AiContracts.QuizQuestion>>(quiz.Questions, JsonOptions);

        return questions ?? throw new BusinessRuleException(
            "Данные теста повреждены.", "study.quiz_corrupted");
    }

    private static QuizResponse ToQuizResponse(Quiz quiz)
    {
        var questions = DeserializeQuestions(quiz);

        return new QuizResponse(
            quiz.Id,
            quiz.StudyMaterialId,
            questions
                .Select(q => new QuizQuestionResponse(q.Question, q.Options, q.Explanation))
                .ToList(),
            quiz.TotalQuestions,
            quiz.Score,
            quiz.CreatedAt);
    }

}
