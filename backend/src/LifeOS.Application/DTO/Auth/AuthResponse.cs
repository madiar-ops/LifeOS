namespace LifeOS.Application.DTO.Auth;

/// <summary>
/// Ответ на успешную аутентификацию.
/// Пара токенов + профиль, чтобы фронт не делал лишний запрос /auth/me сразу после входа.
/// </summary>
public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAt,
    UserResponse User);
