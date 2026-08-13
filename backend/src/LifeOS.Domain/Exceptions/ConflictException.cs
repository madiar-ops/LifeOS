namespace LifeOS.Domain.Exceptions;

/// <summary>Нарушение уникальности или конфликт состояния → HTTP 409.</summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string message, string code = "resource.conflict") : base(message)
        => Code = code;

    public override string Code { get; }
}
