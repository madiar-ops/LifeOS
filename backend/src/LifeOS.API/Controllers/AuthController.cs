using LifeOS.API.Filters;
using LifeOS.Application.DTO.Auth;
using LifeOS.Application.Interfaces.Auth;
using LifeOS.Application.Interfaces.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeOS.API.Controllers;

/// <summary>Регистрация, вход, обновление токенов и выход.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[ServiceFilter(typeof(ValidationFilter))]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICurrentUserService _currentUser;

    public AuthController(IAuthService authService, ICurrentUserService currentUser)
    {
        _authService = authService;
        _currentUser = currentUser;
    }

    /// <summary>Регистрация нового пользователя. Сразу возвращает пару токенов.</summary>
    /// <response code="200">Пользователь создан, токены выданы.</response>
    /// <response code="400">Данные не прошли валидацию.</response>
    /// <response code="409">Email уже занят.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest request, CancellationToken cancellationToken)
        => Ok(await _authService.RegisterAsync(request, cancellationToken));

    /// <summary>Вход по email и паролю.</summary>
    /// <response code="200">Аутентификация успешна.</response>
    /// <response code="400">Неверный email или пароль.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request, CancellationToken cancellationToken)
        => Ok(await _authService.LoginAsync(request, cancellationToken));

    /// <summary>
    /// Обновление пары токенов. Старый refresh-токен гасится (ротация).
    /// Предъявление уже погашенного токена трактуется как компрометация
    /// и отзывает все токены пользователя.
    /// </summary>
    /// <response code="200">Выдана новая пара токенов.</response>
    /// <response code="400">Токен недействителен, истёк или скомпрометирован.</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromBody] RefreshRequest request, CancellationToken cancellationToken)
        => Ok(await _authService.RefreshAsync(request, cancellationToken));

    /// <summary>Выход: гасит переданный refresh-токен. Операция идемпотентна.</summary>
    /// <response code="204">Выход выполнен.</response>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(
        [FromBody] RefreshRequest request, CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }

    /// <summary>Профиль текущего пользователя. Требует действующий access-токен.</summary>
    /// <response code="200">Профиль получен.</response>
    /// <response code="401">Токен отсутствует или недействителен.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> Me(CancellationToken cancellationToken)
        => Ok(await _authService.GetCurrentUserAsync(_currentUser.GetRequiredUserId(), cancellationToken));
}
