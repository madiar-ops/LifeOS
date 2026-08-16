namespace LifeOS.Application.Interfaces.Infrastructure;

/// <summary>
/// Извлечение текста из документа. Абстракция нужна, чтобы слой Application
/// не зависел от конкретной PDF-библиотеки и её типов.
/// </summary>
public interface IDocumentTextExtractor
{
    /// <summary>Возвращает текст PDF. Пустая строка — документ без текстового слоя (скан).</summary>
    Task<string> ExtractTextAsync(Stream content, CancellationToken cancellationToken = default);
}
