using LifeOS.Application.Common;
using LifeOS.Domain.Enums;

namespace LifeOS.Application.DTO.Finance;

public class TransactionQueryParams : PaginationParams
{
    public TransactionType? Type { get; set; }
    public string? Category { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}
