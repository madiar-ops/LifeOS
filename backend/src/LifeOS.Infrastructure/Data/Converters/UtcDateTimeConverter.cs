using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LifeOS.Infrastructure.Data.Converters;

/// <summary>
/// Npgsql сопоставляет DateTime с типом 'timestamp with time zone' и требует,
/// чтобы Kind был Utc — иначе при записи прилетает исключение в рантайме.
/// Клиент присылает даты (например, Deadline) с Kind = Unspecified.
///
/// Конвертер закрывает эту дыру централизованно: в БД всегда уходит UTC,
/// из БД всегда приходит DateTime с Kind = Utc.
/// </summary>
public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            v => v.Kind == DateTimeKind.Utc ? v : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    { }
}

/// <summary>То же самое для nullable-дат.</summary>
public class UtcNullableDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public UtcNullableDateTimeConverter()
        : base(
            v => v.HasValue
                ? (v.Value.Kind == DateTimeKind.Utc ? v.Value : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc))
                : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
    { }
}
