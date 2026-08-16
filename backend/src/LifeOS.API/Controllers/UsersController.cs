using LifeOS.API.Filters;
using LifeOS.Application.DTO.Auth;
using LifeOS.Application.DTO.Users;
using LifeOS.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeOS.API.Controllers;

/// <summary>Профиль пользователя.</summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
[ServiceFilter(typeof(ValidationFilter))]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IFileService _fileService;

    public UsersController(IUserService userService, IFileService fileService)
    {
        _userService = userService;
        _fileService = fileService;
    }

    /// <summary>Профиль пользователя по Id. Доступен только владельцу.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken cancellationToken)
        => Ok(await _userService.GetByIdAsync(id, cancellationToken));

    /// <summary>Изменение имени и фамилии.</summary>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserResponse>> UpdateProfile(
        [FromBody] UpdateProfileRequest request, CancellationToken cancellationToken)
        => Ok(await _userService.UpdateProfileAsync(request, cancellationToken));

    /// <summary>
    /// Загрузка аватара. Принимает JPEG, PNG или WebP до 2 МБ.
    /// Предыдущий аватар удаляется из хранилища.
    /// </summary>
    /// <response code="200">Аватар обновлён, возвращается его URL.</response>
    /// <response code="400">Недопустимый тип или размер файла.</response>
    [HttpPut("avatar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadAvatar(IFormFile file, CancellationToken cancellationToken)
    {
        var upload = await FilesController.ToUploadDataAsync(file, cancellationToken);
        var url = await _fileService.UploadAvatarAsync(upload, cancellationToken);

        return Ok(new { avatarUrl = url });
    }

    /// <summary>
    /// Смена пароля. Требует текущий пароль.
    /// После успешной смены все сессии пользователя отзываются.
    /// </summary>
    [HttpPut("password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        await _userService.ChangePasswordAsync(request, cancellationToken);
        return NoContent();
    }
}
