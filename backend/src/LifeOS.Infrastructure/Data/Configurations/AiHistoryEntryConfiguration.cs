using LifeOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Data.Configurations;

public class AiHistoryEntryConfiguration : IEntityTypeConfiguration<AiHistoryEntry>
{
    public void Configure(EntityTypeBuilder<AiHistoryEntry> builder)
    {
        builder.ToTable("AIHistory");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Endpoint).IsRequired().HasMaxLength(100);
        builder.Property(a => a.RequestPayload).IsRequired().HasColumnType("jsonb");
        builder.Property(a => a.ResponsePayload).IsRequired().HasColumnType("jsonb");
        builder.Property(a => a.Confidence).HasPrecision(4, 3);

        builder.HasOne(a => a.User).WithMany(u => u.AiHistory)
            .HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.UserId, a.CreatedAt }).HasDatabaseName("IX_AIHistory_UserId_CreatedAt");
    }
}
