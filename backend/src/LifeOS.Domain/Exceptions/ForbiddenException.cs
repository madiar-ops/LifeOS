namespace LifeOS.Domain.Exceptions;

/// <summary>
/// Пользователь аутентифицирован, но не владеет ресурсом → HTTP 403.
/// Ключевая защита от IDOR: чужой UserId не должен читать чужие данные.
/// </summary>
public sealed class ForbiddenException : DomainException
{
    public ForbiddenException(string message = "Доступ к этому ресурсу запрещён.") : base(message) { }

    public override string Code => "access.forbidden";
}
