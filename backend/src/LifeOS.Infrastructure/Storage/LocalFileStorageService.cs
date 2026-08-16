using LifeOS.Application.Common;
using LifeOS.Application.Files;
using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LifeOS.Infrastructure.Storage;

/// <summary>
/// Хранилище на диске приложения — запасной провайдер для разработки.
///
/// Нужен, чтобы работа над модулями Study, Career и Profile не блокировалась
/// настройкой Firebase. В проде НЕ используется: на облачных платформах
/// (Render, Vercel) файловая система эфемерна и очищается при рестарте.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly FileStorageSettings _settings;
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly string _rootPath;

    public LocalFileStorageService(
        IOptions<FileStorageSettings> settings,
        ILogger<LocalFileStorageService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        _rootPath = Path.Combine(AppContext.BaseDirectory, _settings.LocalFolder);
        Directory.CreateDirectory(_rootPath);

        _logger.LogWarning(
            "Активно ЛОКАЛЬНОЕ файловое хранилище ({Path}). " +
            "Для production настройте FileStorage:Bucket (Firebase).", _rootPath);
    }

    public string ProviderName => "LocalDisk";

    public async Task<StorageUploadResult> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        Guid userId,
        ModuleType module,
        CancellationToken cancellationToken = default)
    {
        var storagePath = StoragePathBuilder.Build(userId, module, fileName);
        var fullPath = Path.Combine(_rootPath, storagePath.Replace('/', Path.DirectorySeparatorChar));

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        if (content.CanSeek) content.Position = 0;

        await using (var target = File.Create(fullPath))
        {
            await content.CopyToAsync(target, cancellationToken);
        }

        // Возвращаем относительный URL: абсолютный адрес хоста меняется
        // между localhost, preview и prod — пусть его подставляет фронтенд.
        var url = $"{_settings.LocalUrlPrefix.TrimEnd('/')}/{storagePath}";

        return new StorageUploadResult(url, storagePath);
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(_rootPath, storagePath.Replace('/', Path.DirectorySeparatorChar));

        // Отсутствие файла — не ошибка: цель операции в том, чтобы его не было.
        if (File.Exists(fullPath))
            File.Delete(fullPath);

        return Task.CompletedTask;
    }
}
