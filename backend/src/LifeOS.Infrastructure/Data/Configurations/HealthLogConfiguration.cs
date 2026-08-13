using LifeOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Data.Configurations;

public class HealthLogConfiguration : IEntityTypeConfiguration<HealthLog>
{
    public void Configure(EntityTypeBuilder<HealthLog> builder)
    {
        builder.ToTable("HealthLogs");
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Weight).HasPrecision(5, 2);      // до 999.99 кг
        builder.Property(h => h.SleepHours).HasPrecision(4, 2);  // до 99.99 ч
        builder.Property(h => h.Mood).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.Date).IsRequired();

        builder.HasOne(h => h.User).WithMany(u => u.HealthLogs)
            .HasForeignKey(h => h.UserId).OnDelete(DeleteBehavior.Cascade);

        // Одна запись здоровья на пользователя в день — иначе временной ряд
        // для AI-прогноза становится неоднозначным.
        builder.HasIndex(h => new { h.UserId, h.Date }).IsUnique()
            .HasDatabaseName("IX_HealthLogs_UserId_Date");
    }
}
