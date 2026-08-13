using LifeOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Data.Configurations;

public class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.ToTable("Goals");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Title).IsRequired().HasMaxLength(200);
        builder.Property(g => g.Description).HasMaxLength(2000);

        builder.Property(g => g.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(g => g.Priority).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.HasOne(g => g.User).WithMany(u => u.Goals)
            .HasForeignKey(g => g.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(g => g.UserId).HasDatabaseName("IX_Goals_UserId");
        builder.HasIndex(g => new { g.UserId, g.Status }).HasDatabaseName("IX_Goals_UserId_Status");
    }
}
