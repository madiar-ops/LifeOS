using LifeOS.Domain.Common;

namespace LifeOS.Domain.Entities;

/// <summary>
/// Refresh-токен с ротацией. При обновлении старый токен помечается IsRevoked
/// и через ReplacedByToken указывает на новый — это позволяет обнаружить
/// повторное использование украденного токена и отозвать всю цепочку.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; set; }

    /// <summary>Криптостойкая случайная строка (не JWT).</summary>
    public string Token { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAt { get; set; }

    /// <summary>Токен, которым был заменён этот при ротации.</summary>
    public string? ReplacedByToken { get; set; }

    public User User { get; set; } = null!;

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>Токен пригоден к использованию только если не отозван и не истёк.</summary>
    public bool IsActive => !IsRevoked && !IsExpired;
}
