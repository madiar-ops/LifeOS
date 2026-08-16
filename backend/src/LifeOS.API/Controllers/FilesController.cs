using LifeOS.Application.DTO.Common;
using LifeOS.Application.DTO.Files;
using LifeOS.Application.Files;
using LifeOS.Application.Interfaces.Services;
using LifeOS.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeOS.API.Controllers;

/// <summary>Файлы пользователя. Хранятся в Firebase Storage, в БД — метаданные.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class FilesController : ControllerBase
{
    private readonly IFileService _fileService;

    public FilesController(IFileService fileService) => _fileService = fileService;

    /// <summary>Список файлов с фильтром по модулю.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<FileResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<FileResponse>>> GetAll(
        [FromQuery] FileQueryParams query, CancellationToken cancellationToken)
        => Ok((await _fileService.GetAllAsync(query, cancellationToken)).ToResponse());

    /// <summary>Метаданные файла по Id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FileResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _fileService.GetByIdAsync(id, cancellationToken));

    /// <summary>
    /// Загрузка файла. Допустимые типы зависят от модуля:
    /// Study и Career принимают только PDF, Avatar — только изображения.
    /// </summary>
    /// <response code="201">Файл загружен.</response>
    /// <response code="400">Пустой файл, превышен размер или недопустимый тип.</response>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(FileResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FileResponse>> Upload(
        IFormFile file,
        [FromQuery] ModuleType module = ModuleType.General,
        CancellationToken cancellationToken = default)
    {
        var upload = await ToUploadDataAsync(file, cancellationToken);
        var result = await _fileService.UploadAsync(upload, module, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Удаление файла. Вернёт 409, если файл используется другим модулем.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _fileService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Переводит IFormFile в независимый от ASP.NET тип.
    /// Содержимое копируется в память: файлы ограничены 10 МБ, поэтому это
    /// безопасно и позволяет перечитать поток при проверке сигнатуры.
    /// </summary>
    internal static async Task<FileUploadData> ToUploadDataAsync(
        IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new Domain.Exceptions.BusinessRuleException("Файл не передан.", "file.missing");

        var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        return new FileUploadData(
            buffer,
            file.FileName,
            file.ContentType ?? "application/octet-stream",
            file.Length);
    }
}
