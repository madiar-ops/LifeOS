namespace LifeOS.Domain.Exceptions;

/// <summary>Нарушено бизнес-правило домена → HTTP 400.</summary>
public sealed class BusinessRuleException : DomainException
{
    public BusinessRuleException(string message, string code = "business_rule.violated") : base(message)
        => Code = code;

    public override string Code { get; }
}
