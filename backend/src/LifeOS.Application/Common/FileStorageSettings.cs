namespace LifeOS.Application.Common;

/// <summary>
/// Настройки файлового хранилища (секция "FileStorage").
///
/// Поддерживаются два провайдера:
/// • Firebase Storage — основной, используется в проде;
/// • локальная папка — запасной вариант для разработки, чтобы не блокировать
///   работу над модулями, пока Firebase не настроен.
/// Выбор происходит автоматически: если Bucket не задан, включается локальный.
/// </summary>
public class FileStorageSettings
{
    public const string SectionName = "FileStorage";

    /// <summary>Имя bucket в Firebase, обычно вида "my-project.appspot.com".</summary>
    public string? Bucket { get; set; }

    /// <summary>Путь к JSON сервисного аккаунта (удобно локально).</summary>
    public string? CredentialsPath { get; set; }

    /// <summary>
    /// Содержимое JSON сервисного аккаунта строкой.
    /// Нужно для облачного деплоя, где файл положить некуда — только переменная окружения.
    /// </summary>
    public string? CredentialsJson { get; set; }

    /// <summary>Принудительно использовать локальное хранилище, даже если Bucket задан.</summary>
    public bool ForceLocal { get; set; }

    /// <summary>Папка локального хранилища относительно корня приложения.</summary>
    public string LocalFolder { get; set; } = "wwwroot/uploads";

    /// <summary>Публичный префикс URL для локальных файлов.</summary>
    public string LocalUrlPrefix { get; set; } = "/uploads";

    /// <summary>Максимальный размер одного файла в мегабайтах.</summary>
    public int MaxFileSizeMb { get; set; } = 10;

    public bool UseLocal => ForceLocal || string.IsNullOrWhiteSpace(Bucket);
}
