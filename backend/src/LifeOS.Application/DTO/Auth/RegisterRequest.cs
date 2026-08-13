namespace LifeOS.Application.DTO.Auth;

/// <summary>Данные для регистрации нового пользователя.</summary>
public record RegisterRequest(
    string Name,
    string Surname,
    string Email,
    string Password);
