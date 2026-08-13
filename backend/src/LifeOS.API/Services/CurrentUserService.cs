using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Domain.Enums;

namespace LifeOS.API.Services;

/// <summary>
/// Читает данные текущего пользователя из JWT-клеймов.
/// Живёт в слое API, потому что это единственный слой, знающий про HttpContext.
/// Слои Application и Domain видят только интерфейс ICurrentUserService.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            // ASP.NET по умолчанию переименовывает "sub" в NameIdentifier,
            // но это поведение отключается настройкой. Проверяем оба варианта.
            var raw = Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);

            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    public string? Email =>
        Principal?.FindFirstValue(ClaimTypes.Email)
        ?? Principal?.FindFirstValue(JwtRegisteredClaimNames.Email);

    public UserRole? Role
    {
        get
        {
            var raw = Principal?.FindFirstValue(ClaimTypes.Role);
            return Enum.TryParse<UserRole>(raw, out var role) ? role : null;
        }
    }

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid GetRequiredUserId()
        => UserId ?? throw new UnauthorizedAccessException(
            "Идентификатор пользователя отсутствует в токене.");
}
