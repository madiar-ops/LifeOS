using LifeOS.Application.Interfaces.Infrastructure;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace LifeOS.Infrastructure.Ai;

/// <summary>
/// Извлечение текста из PDF через PdfPig.
///
/// Почему PdfPig: чистая C#-библиотека под лицензией Apache 2.0, без нативных
/// зависимостей — важно для деплоя в Linux-контейнер, где iTextSharp и
/// подобные потребовали бы дополнительной возни, а их лицензии несовместимы
/// с бесплатным использованием.
/// </summary>
public class PdfTextExtractor : IDocumentTextExtractor
{
    private readonly ILogger<PdfTextExtractor> _logger;

    /// <summary>
    /// Ограничение объёма: AI-сервис всё равно обрезает вход, а тащить
    /// мегабайты текста по сети и держать их в памяти незачем.
    /// </summary>
    private const int MaxCharacters = 60_000;

    public PdfTextExtractor(ILogger<PdfTextExtractor> logger) => _logger = logger;

    public Task<string> ExtractTextAsync(Stream content, CancellationToken cancellationToken = default)
    {
        if (content.CanSeek) content.Position = 0;

        var builder = new System.Text.StringBuilder();

        using var document = PdfDocument.Open(content);

        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();

            // NearestNeighbourWordExtractor собирает слова по расположению
            // глифов: у PDF нет понятия «слово», текст хранится как набор
            // символов с координатами, и наивное чтение даёт склейку.
            var words = NearestNeighbourWordExtractor.Instance.GetWords(page.Letters);
            var pageText = string.Join(" ", words.Select(w => w.Text));

            if (string.IsNullOrWhiteSpace(pageText))
                continue;

            builder.AppendLine(pageText);

            if (builder.Length >= MaxCharacters)
            {
                _logger.LogInformation(
                    "Достигнут предел извлечения ({Max} символов), остальные страницы пропущены.",
                    MaxCharacters);
                break;
            }
        }

        var text = builder.ToString().Trim();

        if (text.Length > MaxCharacters)
            text = text[..MaxCharacters];

        return Task.FromResult(text);
    }
}
