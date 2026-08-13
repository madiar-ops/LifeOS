using LifeOS.Domain.Enums;

namespace LifeOS.Application.DTO.Auth;

/// <summary>
/// Публичное представление пользователя.
/// PasswordHash сюда не входит физически — это гарантия, а не договорённость.
/// </summary>
public record UserResponse(
    Guid Id,
    string Name,
    string Surname,
    string Email,
    string? AvatarUrl,
    UserRole Role,
    DateTime CreatedAt);
