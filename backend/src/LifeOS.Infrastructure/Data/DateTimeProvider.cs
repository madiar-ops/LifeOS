using LifeOS.Application.Interfaces.Infrastructure;

namespace LifeOS.Infrastructure.Data;

/// <inheritdoc />
public sealed class DateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);
}
