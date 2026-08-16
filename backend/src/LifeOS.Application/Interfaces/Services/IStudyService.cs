using LifeOS.Application.Common;
using LifeOS.Application.DTO.Ai;
using LifeOS.Application.DTO.Study;

namespace LifeOS.Application.Interfaces.Services;

public interface IStudyService
{
    Task<PagedResult<StudyMaterialResponse>> GetMaterialsAsync(
        PaginationParams pagination, CancellationToken cancellationToken = default);

    Task<StudyMaterialResponse> GetMaterialAsync(Guid id, CancellationToken cancellationToken = default);

    Task<StudyMaterialResponse> CreateMaterialAsync(
        CreateStudyMaterialRequest request, CancellationToken cancellationToken = default);

    Task DeleteMaterialAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Извлекает текст из PDF, отправляет в AI и сохраняет конспект.</summary>
    Task<AiResultResponse<StudySummaryResult>> SummarizeAsync(
        Guid materialId, CancellationToken cancellationToken = default);

    Task<AiResultResponse<QuizResponse>> GenerateQuizAsync(
        GenerateQuizRequest request, CancellationToken cancellationToken = default);

    Task<QuizResponse> GetQuizAsync(Guid quizId, CancellationToken cancellationToken = default);

    Task<QuizGradeResponse> SubmitQuizAsync(
        Guid quizId, SubmitQuizRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudyNoteResponse>> GetNotesAsync(
        Guid materialId, CancellationToken cancellationToken = default);

    Task<StudyNoteResponse> CreateNoteAsync(
        CreateStudyNoteRequest request, CancellationToken cancellationToken = default);

    Task<StudyNoteResponse> UpdateNoteAsync(
        Guid id, UpdateStudyNoteRequest request, CancellationToken cancellationToken = default);

    Task DeleteNoteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>Конспект вместе с ключевыми пунктами.</summary>
public record StudySummaryResult(string Summary, IReadOnlyList<string> KeyPoints, string Source);
