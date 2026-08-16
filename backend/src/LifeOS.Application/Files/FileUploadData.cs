namespace LifeOS.Application.Files;

/// <summary>
/// Файл в виде, не зависящем от ASP.NET.
/// Слой Application не должен знать про IFormFile — иначе сервисы
/// нельзя было бы вызвать из фонового задания или из тестов.
/// </summary>
public record FileUploadData(
    Stream Content,
    string FileName,
    string ContentType,
    long Length);
