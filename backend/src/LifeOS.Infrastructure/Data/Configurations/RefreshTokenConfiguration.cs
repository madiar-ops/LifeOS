using LifeOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Token).IsRequired().HasMaxLength(200);
        builder.Property(t => t.ReplacedByToken).HasMaxLength(200);
        builder.Property(t => t.ExpiresAt).IsRequired();

        // Вычисляемые свойства не хранятся в БД.
        builder.Ignore(t => t.IsActive);
        builder.Ignore(t => t.IsExpired);

        // Поиск при /auth/refresh идёт по значению токена — индекс обязателен.
        builder.HasIndex(t => t.Token).IsUnique().HasDatabaseName("IX_RefreshTokens_Token");
        builder.HasIndex(t => t.UserId).HasDatabaseName("IX_RefreshTokens_UserId");
    }
}
