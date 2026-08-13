namespace LifeOS.Application.DTO.Users;

/// <summary>Смена пароля. Текущий пароль обязателен — защита от угона активной сессии.</summary>
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
