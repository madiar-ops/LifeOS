using LifeOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Data.Configurations;

public class RecommendationConfiguration : IEntityTypeConfiguration<Recommendation>
{
    public void Configure(EntityTypeBuilder<Recommendation> builder)
    {
        builder.ToTable("Recommendations");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Module).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Content).IsRequired().HasColumnType("text");
        builder.Property(r => r.Confidence).IsRequired().HasPrecision(4, 3); // 0.000–1.000

        builder.HasOne(r => r.User).WithMany(u => u.Recommendations)
            .HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.UserId, r.Module }).HasDatabaseName("IX_Recommendations_UserId_Module");
    }
}
