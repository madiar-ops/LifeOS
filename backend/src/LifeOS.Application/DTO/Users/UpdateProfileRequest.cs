namespace LifeOS.Application.DTO.Users;

/// <summary>
/// Изменение профиля. Email и Role сюда намеренно НЕ входят:
/// смена email требует подтверждения, смена роли — операция администратора.
/// </summary>
public record UpdateProfileRequest(string Name, string Surname);
