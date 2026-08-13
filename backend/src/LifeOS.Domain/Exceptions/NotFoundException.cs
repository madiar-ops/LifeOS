namespace LifeOS.Domain.Exceptions;

/// <summary>Сущность не найдена → HTTP 404.</summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} с идентификатором '{key}' не найден.") { }

    public NotFoundException(string message) : base(message) { }

    public override string Code => "resource.not_found";
}
