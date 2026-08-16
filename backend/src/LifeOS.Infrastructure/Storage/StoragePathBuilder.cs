using LifeOS.Domain.Enums;

namespace LifeOS.Infrastructure.Storage;

/// <summary>
/// Формирование пути объекта в хранилище. Общий код для всех провайдеров,
/// чтобы структура папок не разъезжалась между Firebase и локальной папкой.
/// </summary>
internal static class StoragePathBuilder
{
    /// <summary>
    /// Схема: users/{userId}/{module}/{guid}{ext}
    ///
    /// • userId в пути — сразу видно, чьи файлы, и легко удалить всё при удалении аккаунта;
    /// • имя файла заменяется на GUID — исключает коллизии имён и path traversal;
    /// • расширение сохраняется — по нему хранилище отдаёт правильный Content-Type.
    /// </summary>
    public static string Build(Guid userId, ModuleType module, string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);

        if (string.IsNullOrWhiteSpace(extension) || extension.Length > 10)
            extension = string.Empty;

        var moduleFolder = module.ToString().ToLowerInvariant();

        return $"users/{userId}/{moduleFolder}/{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
    }
}
