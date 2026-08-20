using LifeOS.API.Extensions;
using LifeOS.API.Filters;
using LifeOS.Application.DTO.Ai;
using LifeOS.Application.DTO.Career;
using LifeOS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LifeOS.API.Controllers;

/// <summary>Карьерный модуль: профиль и AI-разбор резюме.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
[ServiceFilter(typeof(ValidationFilter))]
public class CareerController : ControllerBase
{
    private readonly ICareerService _careerService;

    public CareerController(ICareerService careerService) => _careerService = careerService;

    /// <summary>Карьерный профиль. Создаётся автоматически при первом обращении.</summary>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(CareerProfileResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CareerProfileResponse>> GetProfile(CancellationToken cancellationToken)
        => Ok(await _careerService.GetProfileAsync(cancellationToken));

    /// <summary>
    /// Обновление профиля. ResumeFileId должен указывать на ранее
    /// загруженный PDF (POST /api/files/upload?module=Career).
    /// </summary>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(CareerProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CareerProfileResponse>> UpdateProfile(
        [FromBody] UpdateCareerProfileRequest request, CancellationToken cancellationToken)
        => Ok(await _careerService.UpdateProfileAsync(request, cancellationToken));

    /// <summary>
    /// AI-разбор резюме: сильные и слабые стороны, недостающие навыки.
    /// Результат сохраняется в профиле.
    /// </summary>
    /// <response code="400">Резюме не загружено или в PDF нет текстового слоя.</response>
    [HttpPost("resume-analysis")]
    [EnableRateLimiting(RateLimitingExtensions.AiPolicy)]
    [ProducesResponseType(typeof(AiResultResponse<ResumeAnalysisResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AiResultResponse<ResumeAnalysisResponse>>> AnalyzeResume(
        CancellationToken cancellationToken)
        => Ok(await _careerService.AnalyzeResumeAsync(cancellationToken));
}
