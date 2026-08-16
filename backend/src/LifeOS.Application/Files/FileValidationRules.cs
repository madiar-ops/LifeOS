using LifeOS.Domain.Enums;

namespace LifeOS.Application.Files;

/// <summary>
/// Правила приёма файлов. Вынесены отдельно, чтобы ограничения были
/// в одном месте, а не размазаны по сервисам и контроллерам.
/// </summary>
public static class FileValidationRules
{
    /// <summary>
    /// Разрешённые MIME-типы по модулям.
    /// Study и Career принимают только PDF: их содержимое уходит на разбор
    /// в AI-сервис, и произвольные форматы там просто не поддерживаются.
    /// </summary>
    public static readonly IReadOnlyDictionary<ModuleType, string[]> AllowedContentTypes =
        new Dictionary<ModuleType, string[]>
        {
            [ModuleType.Avatar] = new[] { "image/jpeg", "image/png", "image/webp" },
            [ModuleType.Study] = new[] { "application/pdf" },
            [ModuleType.Career] = new[] { "application/pdf" },
            [ModuleType.General] = new[]
            {
                "application/pdf", "image/jpeg", "image/png", "image/webp", "text/plain"
            },
            [ModuleType.Finance] = new[] { "application/pdf", "image/jpeg", "image/png" },
            [ModuleType.Health] = new[] { "application/pdf", "image/jpeg", "image/png" }
        };

    /// <summary>Расширения, соответствующие разрешённым типам.</summary>
    public static readonly IReadOnlyDictionary<string, string[]> ExtensionsByContentType =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"] = new[] { ".pdf" },
            ["image/jpeg"] = new[] { ".jpg", ".jpeg" },
            ["image/png"] = new[] { ".png" },
            ["image/webp"] = new[] { ".webp" },
            ["text/plain"] = new[] { ".txt" }
        };

    /// <summary>Отдельный, более жёсткий лимит на аватар — это картинка профиля, не документ.</summary>
    public const int AvatarMaxSizeMb = 2;

    /// <summary>
    /// Сигнатуры («магические числа») начала файла.
    ///
    /// Заголовок Content-Type присылает клиент, и подделать его тривиально:
    /// достаточно переименовать exe в pdf. Проверка первых байтов —
    /// единственный способ убедиться, что внутри действительно то, что заявлено.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, byte[][]> Signatures =
        new Dictionary<string, byte[][]>(StringComparer.OrdinalIgnoreCase)
        {
            // "%PDF"
            ["application/pdf"] = new[] { new byte[] { 0x25, 0x50, 0x44, 0x46 } },

            // JPEG всегда начинается с FF D8 FF
            ["image/jpeg"] = new[] { new byte[] { 0xFF, 0xD8, 0xFF } },

            // PNG: 89 50 4E 47 0D 0A 1A 0A
            ["image/png"] = new[]
            {
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }
            },

            // WebP: "RIFF" .... "WEBP" — проверяем префикс RIFF
            ["image/webp"] = new[] { new byte[] { 0x52, 0x49, 0x46, 0x46 } }

            // text/plain намеренно без сигнатуры: у обычного текста её не существует.
        };
}
