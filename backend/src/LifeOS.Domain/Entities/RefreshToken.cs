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

    /// <summary>
    /// Истёк ли токен на указанный момент.
    ///
    /// Время передаётся аргументом, а не берётся из DateTime.UtcNow: обращение
    /// к системным часам прямо из доменной сущности делает её непроверяемой —
    /// тест на истечение токена пришлось бы ждать семь дней. Источник времени
    /// в приложении один — IDateTimeProvider.
    /// </summary>
    public bool IsExpiredAt(DateTime utcNow) => utcNow >= ExpiresAt;

    /// <summary>Токен пригоден к использованию, только если не отозван и не истёк.</summary>
    public bool IsActiveAt(DateTime utcNow) => !IsRevoked && !IsExpiredAt(utcNow);
}
