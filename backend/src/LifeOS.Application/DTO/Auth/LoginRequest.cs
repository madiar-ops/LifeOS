namespace LifeOS.Application.DTO.Auth;

/// <summary>Учётные данные для входа.</summary>
public record LoginRequest(string Email, string Password);
