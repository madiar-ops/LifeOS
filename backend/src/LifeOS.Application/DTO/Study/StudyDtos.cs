namespace LifeOS.Application.DTO.Study;

public record StudyMaterialResponse(
    Guid Id,
    Guid FileId,
    string Title,
    string? Summary,
    string FileName,
    string FileUrl,
    int NotesCount,
    int QuizzesCount,
    DateTime CreatedAt);

/// <summary>
/// Создание материала из УЖЕ загруженного файла.
/// Загрузка идёт отдельным вызовом /api/files/upload — так модуль Study
/// не дублирует валидацию файлов, а переиспользует её.
/// </summary>
public record CreateStudyMaterialRequest(Guid FileId, string Title);

public record StudyNoteResponse(
    Guid Id,
    Guid StudyMaterialId,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateStudyNoteRequest(Guid StudyMaterialId, string Content);

public record UpdateStudyNoteRequest(string Content);

public record QuizQuestionResponse(
    string Question,
    IReadOnlyList<string> Options,
    string Explanation);

/// <summary>
/// Вопросы БЕЗ правильных ответов — иначе тест решался бы через DevTools.
/// Проверка происходит на сервере в SubmitQuizAsync.
/// </summary>
public record QuizResponse(
    Guid Id,
    Guid StudyMaterialId,
    IReadOnlyList<QuizQuestionResponse> Questions,
    int TotalQuestions,
    int? Score,
    DateTime CreatedAt);

public record GenerateQuizRequest(Guid StudyMaterialId, int QuestionCount);

public record SubmitQuizRequest(IReadOnlyList<int> Answers);

public record QuizGradeResponse(
    Guid QuizId,
    int Score,
    int TotalQuestions,
    IReadOnlyList<QuizAnswerResult> Results);

public record QuizAnswerResult(
    int QuestionIndex,
    int SubmittedIndex,
    int CorrectIndex,
    bool IsCorrect,
    string Explanation);
