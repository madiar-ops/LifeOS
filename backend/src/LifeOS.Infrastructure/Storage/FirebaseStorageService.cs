using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using LifeOS.Application.Common;
using LifeOS.Application.Files;
using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LifeOS.Infrastructure.Storage;

/// <summary>
/// Firebase Storage. Под капотом это Google Cloud Storage, поэтому работаем
/// официальным клиентом Google.Cloud.Storage.V1 — он поддерживает потоковую
/// загрузку и не требует Firebase Admin SDK ради одной операции.
/// </summary>
public class FirebaseStorageService : IFileStorageService
{
    private readonly StorageClient _client;
    private readonly FileStorageSettings _settings;
    private readonly ILogger<FirebaseStorageService> _logger;

    public FirebaseStorageService(
        IOptions<FileStorageSettings> settings,
        ILogger<FirebaseStorageService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        _client = StorageClient.Create(LoadCredential(_settings));

        _logger.LogInformation("Файловое хранилище: Firebase, bucket {Bucket}", _settings.Bucket);
    }

    public string ProviderName => "FirebaseStorage";

    public async Task<StorageUploadResult> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        Guid userId,
        ModuleType module,
        CancellationToken cancellationToken = default)
    {
        var storagePath = StoragePathBuilder.Build(userId, module, fileName);

        if (content.CanSeek) content.Position = 0;

        // downloadTokens делает объект доступным по постоянной ссылке вида
        // firebasestorage.googleapis.com/...?alt=media&token=... — так же,
        // как это делает Firebase SDK на клиенте.
        var downloadToken = Guid.NewGuid().ToString();

        var uploadObject = new Google.Apis.Storage.v1.Data.Object
        {
            Bucket = _settings.Bucket,
            Name = storagePath,
            ContentType = contentType,
            Metadata = new Dictionary<string, string>
            {
                ["firebaseStorageDownloadTokens"] = downloadToken,
                ["userId"] = userId.ToString(),
                ["module"] = module.ToString()
            }
        };

        await _client.UploadObjectAsync(
            uploadObject, content, options: null, cancellationToken: cancellationToken);

        var url = BuildDownloadUrl(_settings.Bucket!, storagePath, downloadToken);

        return new StorageUploadResult(url, storagePath);
    }

    public async Task<Stream> DownloadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        // Скачиваем в память: файлы ограничены 10 МБ, а поток должен быть
        // перечитываемым — PDF-парсер требует возможности перемещаться по нему.
        var buffer = new MemoryStream();

        await _client.DownloadObjectAsync(
            _settings.Bucket, storagePath, buffer,
            options: null, cancellationToken: cancellationToken);

        buffer.Position = 0;
        return buffer;
    }

    public async Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteObjectAsync(_settings.Bucket, storagePath,
                options: null, cancellationToken: cancellationToken);
        }
        catch (Google.GoogleApiException ex)
            when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Объекта уже нет — цель операции достигнута, это не ошибка.
            _logger.LogWarning("Объект {StoragePath} отсутствует в bucket при удалении.", storagePath);
        }
    }

    private static string BuildDownloadUrl(string bucket, string storagePath, string token)
    {
        // Слэши в пути должны быть закодированы — иначе Firebase воспримет
        // их как разделители сегментов URL, а не как часть имени объекта.
        var encodedPath = Uri.EscapeDataString(storagePath);

        return $"https://firebasestorage.googleapis.com/v0/b/{bucket}/o/{encodedPath}" +
               $"?alt=media&token={token}";
    }

    /// <summary>
    /// Учётные данные берутся либо из файла (удобно локально),
    /// либо из строки JSON (единственный вариант для облачного деплоя,
    /// где положить файл на диск некуда — только переменная окружения).
    /// </summary>
    private static GoogleCredential LoadCredential(FileStorageSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.CredentialsJson))
            return GoogleCredential.FromJson(settings.CredentialsJson);

        if (!string.IsNullOrWhiteSpace(settings.CredentialsPath))
        {
            if (!File.Exists(settings.CredentialsPath))
                throw new InvalidOperationException(
                    $"Файл учётных данных Firebase не найден: {settings.CredentialsPath}");

            return GoogleCredential.FromFile(settings.CredentialsPath);
        }

        throw new InvalidOperationException(
            "Не заданы учётные данные Firebase. Укажите FileStorage:CredentialsPath " +
            "или FileStorage:CredentialsJson, либо включите FileStorage:ForceLocal для разработки.");
    }
}
