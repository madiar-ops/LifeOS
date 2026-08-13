using LifeOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Name).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Surname).IsRequired().HasMaxLength(100);

        builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
        builder.Property(u => u.PasswordHash).IsRequired().HasMaxLength(256);
        builder.Property(u => u.AvatarUrl).HasMaxLength(1000);

        // Enum как строка: миграции читаемы, значения в БД самодокументируемы,
        // а вставка нового элемента в середину enum не ломает существующие данные.
        builder.Property(u => u.Role).IsRequired().HasConversion<string>().HasMaxLength(20);

        // Уникальность email не зависит от регистра — email нормализуется в lower-case
        // в AuthService, а уникальный индекс гарантирует это на уровне БД.
        builder.HasIndex(u => u.Email).IsUnique().HasDatabaseName("IX_Users_Email");

        builder.HasMany(u => u.RefreshTokens).WithOne(t => t.User)
            .HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(u => u.CareerProfile).WithOne(c => c.User)
            .HasForeignKey<CareerProfile>(c => c.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
