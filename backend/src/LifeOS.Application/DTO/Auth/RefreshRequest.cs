namespace LifeOS.Application.DTO.Auth;

/// <summary>
/// Запрос на обновление пары токенов.
/// Access-токен не передаётся: он мог уже истечь, и это нормально.
/// </summary>
public record RefreshRequest(string RefreshToken);
