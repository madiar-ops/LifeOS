using LifeOS.Application.Common;
using LifeOS.Application.DTO.Finance;

namespace LifeOS.Application.Interfaces.Services;

public interface IFinanceService
{
    Task<PagedResult<TransactionResponse>> GetAllAsync(TransactionQueryParams query, CancellationToken cancellationToken = default);
    Task<TransactionResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TransactionResponse> CreateAsync(CreateTransactionRequest request, CancellationToken cancellationToken = default);
    Task<TransactionResponse> UpdateAsync(Guid id, UpdateTransactionRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<FinanceSummaryResponse> GetSummaryAsync(
        DateOnly? from, DateOnly? to, string? currency, CancellationToken cancellationToken = default);
}
