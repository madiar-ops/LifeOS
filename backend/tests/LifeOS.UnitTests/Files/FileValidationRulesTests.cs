using FluentAssertions;
using LifeOS.Application.Files;
using LifeOS.Domain.Enums;

namespace LifeOS.UnitTests.Files;

/// <summary>
/// Тесты правил приёма файлов.
///
/// Заголовок Content-Type присылает клиент, и подделать его тривиально —
/// достаточно переименовать исполняемый файл в .pdf. Сигнатуры («магические
/// числа») первых байтов — единственная проверка, которую нельзя обойти
/// переименованием, поэтому она разобрана здесь подробно.
/// </summary>
public class FileValidationRulesTests
{
    [Theory]
    [InlineData(ModuleType.Study)]
    [InlineData(ModuleType.Career)]
    public void Учебные_и_карьерные_материалы_принимаются_только_в_PDF(ModuleType module)
    {
        // Содержимое этих модулей уходит на разбор в AI-сервис,
        // а он умеет извлекать текст только из PDF.
        FileValidationRules.AllowedContentTypes[module].Should().Equal("application/pdf");
    }

    [Fact]
    public void Аватар_принимается_только_как_изображение()
    {
        var allowed = FileValidationRules.AllowedContentTypes[ModuleType.Avatar];

        allowed.Should().BeEquivalentTo("image/jpeg", "image/png", "image/webp");
        allowed.Should().NotContain("application/pdf");
    }

    [Fact]
    public void Для_каждого_разрешённого_типа_задано_расширение()
    {
        var allTypes = FileValidationRules.AllowedContentTypes.Values
            .SelectMany(types => types)
            .Distinct();

        // Пропущенное сопоставление означало бы, что файл проходит проверку
        // MIME-типа и падает на проверке расширения — с невнятной ошибкой.
        foreach (var contentType in allTypes)
            FileValidationRules.ExtensionsByContentType.Should().ContainKey(contentType);
    }

    [Fact]
    public void Расширения_сопоставляются_без_учёта_регистра()
    {
        // Windows отдаёт «Резюме.PDF», Linux — «резюме.pdf». Разное поведение
        // на разных машинах пользователей недопустимо.
        FileValidationRules.ExtensionsByContentType.ContainsKey("APPLICATION/PDF").Should().BeTrue();
    }

    [Theory]
    [InlineData("application/pdf", new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31 })]   // %PDF-1
    [InlineData("image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 })]
    [InlineData("image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })]
    [InlineData("image/webp", new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00 })]
    public void Настоящий_файл_совпадает_со_своей_сигнатурой(string contentType, byte[] header)
    {
        MatchesAnySignature(contentType, header).Should().BeTrue();
    }

    [Fact]
    public void Исполняемый_файл_переименованный_в_pdf_не_проходит_проверку()
    {
        // 4D 5A — «MZ», начало любого PE-файла Windows (.exe, .dll).
        var windowsExecutable = new byte[] { 0x4D, 0x5A, 0x90, 0x00 };

        MatchesAnySignature("application/pdf", windowsExecutable).Should().BeFalse();
    }

    [Fact]
    public void PDF_переименованный_в_картинку_не_проходит_проверку()
    {
        var pdfHeader = new byte[] { 0x25, 0x50, 0x44, 0x46 };

        MatchesAnySignature("image/png", pdfHeader).Should().BeFalse();
        MatchesAnySignature("image/jpeg", pdfHeader).Should().BeFalse();
    }

    [Fact]
    public void Слишком_короткий_файл_не_считается_валидным()
    {
        // Обрезанный до двух байтов PDF не должен «случайно» пройти проверку
        // из-за того, что сравнивать оказалось нечего.
        MatchesAnySignature("application/pdf", new byte[] { 0x25, 0x50 }).Should().BeFalse();
    }

    [Fact]
    public void Текстовый_файл_сознательно_остаётся_без_сигнатуры()
    {
        // У обычного текста магических чисел не существует. Требовать их
        // означало бы отвергать любой .txt — поэтому его здесь нет намеренно.
        FileValidationRules.Signatures.Should().NotContainKey("text/plain");
        FileValidationRules.AllowedContentTypes[ModuleType.General].Should().Contain("text/plain");
    }

    [Fact]
    public void Лимит_на_аватар_строже_общего_лимита_на_файлы()
    {
        // Картинка профиля не документ: 2 МБ достаточно, а больший лимит
        // означал бы мегабайты трафика на каждой отрисовке списка.
        FileValidationRules.AvatarMaxSizeMb.Should().Be(2);
        FileValidationRules.AvatarMaxSizeMb.Should().BeLessThan(10);
    }

    /// <summary>
    /// Повторяет ту же логику сравнения, что и <c>FileService</c>: файл считается
    /// подлинным, если его первые байты совпадают хотя бы с одной сигнатурой типа.
    /// </summary>
    private static bool MatchesAnySignature(string contentType, byte[] header)
    {
        if (!FileValidationRules.Signatures.TryGetValue(contentType, out var signatures))
            return false;

        return signatures.Any(signature =>
            header.Length >= signature.Length &&
            header.Take(signature.Length).SequenceEqual(signature));
    }
}
