using LifeOS.API.Extensions;
using LifeOS.API.Filters;
using LifeOS.Application.Common;
using LifeOS.Application.DTO.Ai;
using LifeOS.Application.DTO.Common;
using LifeOS.Application.DTO.Study;
using LifeOS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LifeOS.API.Controllers;

/// <summary>Учебный модуль: материалы, AI-конспекты, тесты и заметки.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
[ServiceFilter(typeof(ValidationFilter))]
public class StudyController : ControllerBase
{
    private readonly IStudyService _studyService;

    public StudyController(IStudyService studyService) => _studyService = studyService;

    // ---- Материалы -------------------------------------------------------

    /// <summary>Список учебных материалов.</summary>
    [HttpGet("materials")]
    [ProducesResponseType(typeof(PagedResponse<StudyMaterialResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<StudyMaterialResponse>>> GetMaterials(
        [FromQuery] PaginationParams pagination, CancellationToken cancellationToken)
        => Ok((await _studyService.GetMaterialsAsync(pagination, cancellationToken)).ToResponse());

    /// <summary>Материал по Id.</summary>
    [HttpGet("materials/{id:guid}")]
    [ProducesResponseType(typeof(StudyMaterialResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudyMaterialResponse>> GetMaterial(
        Guid id, CancellationToken cancellationToken)
        => Ok(await _studyService.GetMaterialAsync(id, cancellationToken));

    /// <summary>
    /// Создание материала из ранее загруженного PDF.
    /// Файл загружается отдельно через POST /api/files/upload?module=Study.
    /// </summary>
    [HttpPost("materials")]
    [ProducesResponseType(typeof(StudyMaterialResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StudyMaterialResponse>> CreateMaterial(
        [FromBody] CreateStudyMaterialRequest request, CancellationToken cancellationToken)
    {
        var material = await _studyService.CreateMaterialAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetMaterial), new { id = material.Id }, material);
    }

    /// <summary>Удаление материала. Заметки и тесты удаляются каскадом, файл остаётся.</summary>
    [HttpDelete("materials/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteMaterial(Guid id, CancellationToken cancellationToken)
    {
        await _studyService.DeleteMaterialAsync(id, cancellationToken);
        return NoContent();
    }

    // ---- AI --------------------------------------------------------------

    /// <summary>
    /// Генерация конспекта: текст извлекается из PDF и отправляется в AI-сервис.
    /// Результат сохраняется в материале.
    /// </summary>
    /// <response code="400">Нет текстового слоя в PDF или AI-сервис недоступен.</response>
    [HttpPost("materials/{id:guid}/summarize")]
    [EnableRateLimiting(RateLimitingExtensions.AiPolicy)]
    [ProducesResponseType(typeof(AiResultResponse<StudySummaryResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AiResultResponse<StudySummaryResult>>> Summarize(
        Guid id, CancellationToken cancellationToken)
        => Ok(await _studyService.SummarizeAsync(id, cancellationToken));

    /// <summary>Генерация теста по материалу. Требует настроенный ключ LLM в AI-сервисе.</summary>
    [HttpPost("quizzes")]
    [EnableRateLimiting(RateLimitingExtensions.AiPolicy)]
    [ProducesResponseType(typeof(AiResultResponse<QuizResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AiResultResponse<QuizResponse>>> GenerateQuiz(
        [FromBody] GenerateQuizRequest request, CancellationToken cancellationToken)
        => Ok(await _studyService.GenerateQuizAsync(request, cancellationToken));

    /// <summary>Тест по Id. Правильные ответы не возвращаются.</summary>
    [HttpGet("quizzes/{id:guid}")]
    [ProducesResponseType(typeof(QuizResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<QuizResponse>> GetQuiz(Guid id, CancellationToken cancellationToken)
        => Ok(await _studyService.GetQuizAsync(id, cancellationToken));

    /// <summary>Проверка ответов. Оценка считается на сервере.</summary>
    [HttpPost("quizzes/{id:guid}/submit")]
    [ProducesResponseType(typeof(QuizGradeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QuizGradeResponse>> SubmitQuiz(
        Guid id, [FromBody] SubmitQuizRequest request, CancellationToken cancellationToken)
        => Ok(await _studyService.SubmitQuizAsync(id, request, cancellationToken));

    // ---- Заметки ---------------------------------------------------------

    /// <summary>Заметки к материалу.</summary>
    [HttpGet("materials/{materialId:guid}/notes")]
    [ProducesResponseType(typeof(IReadOnlyList<StudyNoteResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StudyNoteResponse>>> GetNotes(
        Guid materialId, CancellationToken cancellationToken)
        => Ok(await _studyService.GetNotesAsync(materialId, cancellationToken));

    /// <summary>Создание заметки.</summary>
    [HttpPost("notes")]
    [ProducesResponseType(typeof(StudyNoteResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<StudyNoteResponse>> CreateNote(
        [FromBody] CreateStudyNoteRequest request, CancellationToken cancellationToken)
        => Ok(await _studyService.CreateNoteAsync(request, cancellationToken));

    /// <summary>Изменение заметки.</summary>
    [HttpPut("notes/{id:guid}")]
    [ProducesResponseType(typeof(StudyNoteResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<StudyNoteResponse>> UpdateNote(
        Guid id, [FromBody] UpdateStudyNoteRequest request, CancellationToken cancellationToken)
        => Ok(await _studyService.UpdateNoteAsync(id, request, cancellationToken));

    /// <summary>Удаление заметки.</summary>
    [HttpDelete("notes/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteNote(Guid id, CancellationToken cancellationToken)
    {
        await _studyService.DeleteNoteAsync(id, cancellationToken);
        return NoContent();
    }
}
