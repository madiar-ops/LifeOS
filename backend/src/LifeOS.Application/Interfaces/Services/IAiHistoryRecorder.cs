using LifeOS.Domain.Enums;

namespace LifeOS.Application.Interfaces.Services;

/// <summary>
/// Аудит обращений к AI и создание рекомендаций.
///
/// Вынесено в отдельный сервис, чтобы каждый модуль не дублировал одну
/// и ту же логику «записать вызов + при высокой уверенности сохранить
/// рекомендацию».
/// </summary>
public interface IAiHistoryRecorder
{
    /// <summary>
    /// Записывает вызов в AIHistory и, если модель была уверена,
    /// сохраняет результат как рекомендацию пользователю.
    /// </summary>
    Task RecordAsync(
        string endpoint,
        object request,
        object response,
        decimal? confidence,
        bool isConfident,
        ModuleType module,
        string? recommendationText = null,
        CancellationToken cancellationToken = default);
}
