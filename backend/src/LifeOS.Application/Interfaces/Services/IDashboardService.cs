using LifeOS.Application.DTO.Dashboard;

namespace LifeOS.Application.Interfaces.Services;

public interface IDashboardService
{
    /// <summary>Сводка главного экрана за указанный период (по умолчанию 30 дней).</summary>
    Task<DashboardResponse> GetAsync(int days, CancellationToken cancellationToken = default);
}
