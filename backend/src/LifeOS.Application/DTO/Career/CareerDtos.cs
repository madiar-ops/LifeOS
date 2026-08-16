namespace LifeOS.Application.DTO.Career;

public record CareerProfileResponse(
    Guid Id,
    Guid? ResumeFileId,
    string? ResumeFileName,
    string? Skills,
    string? DesiredPosition,
    string? AiReview,
    DateTime UpdatedAt);

public record UpdateCareerProfileRequest(
    string? Skills,
    string? DesiredPosition,
    Guid? ResumeFileId);

public record ResumeAnalysisResponse(
    decimal OverallScore,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> Weaknesses,
    IReadOnlyList<string> MissingSkills,
    IReadOnlyList<string> Suggestions,
    string Source);
