namespace LifeOS.Application.DTO.Ai;

/// <summary>Запись аудита обращения к AI. Нужна для отладки и демонстрации на защите.</summary>
public record AiHistoryResponse(
    Guid Id,
    string Endpoint,
    decimal? Confidence,
    DateTime CreatedAt);
