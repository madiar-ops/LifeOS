using LifeOS.Application.Files;
using LifeOS.Domain.Enums;

namespace LifeOS.Application.Interfaces.Infrastructure;

/// <summary>
/// Абстракция файлового хранилища.
/// Слой Application не знает, Firebase это, S3 или локальная папка —
/// благодаря этому провайдер меняется без правки бизнес-логики.
/// </summary>
public interface IFileStorageService
{
    Task<StorageUploadResult> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        Guid userId,
        ModuleType module,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Чтение объекта из хранилища.
    /// Добавлено в Фазе 6: учебному модулю нужно получить PDF обратно,
    /// чтобы извлечь текст и отправить его в AI-сервис.
    /// </summary>
    Task<Stream> DownloadAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаление объекта по внутреннему пути.
    /// Отсутствие объекта не считается ошибкой: цель — чтобы файла не было.
    /// </summary>
    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>Имя активного провайдера — для диагностики и логов.</summary>
    string ProviderName { get; }
}
