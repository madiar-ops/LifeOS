using LifeOS.Domain.Enums;

namespace LifeOS.Application.DTO.Ai;

public record RecommendationResponse(
    Guid Id,
    ModuleType Module,
    string Content,
    decimal Confidence,
    DateTime CreatedAt);
