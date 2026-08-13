using LifeOS.Domain.Enums;

namespace LifeOS.Application.DTO.Finance;

public record TransactionResponse(
    Guid Id,
    TransactionType Type,
    string Category,
    decimal Amount,
    string Currency,
    DateOnly Date,
    string? Description,
    DateTime CreatedAt);
