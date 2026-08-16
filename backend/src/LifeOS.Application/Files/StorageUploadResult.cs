namespace LifeOS.Application.Files;

/// <summary>
/// Результат загрузки в хранилище.
/// StoragePath хранится отдельно от Url, потому что удалять объект
/// нужно именно по пути внутри bucket, а не по публичной ссылке.
/// </summary>
public record StorageUploadResult(string Url, string StoragePath);
