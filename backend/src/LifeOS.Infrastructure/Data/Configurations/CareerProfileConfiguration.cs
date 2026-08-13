using LifeOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Data.Configurations;

public class CareerProfileConfiguration : IEntityTypeConfiguration<CareerProfile>
{
    public void Configure(EntityTypeBuilder<CareerProfile> builder)
    {
        builder.ToTable("CareerProfiles");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Skills).HasMaxLength(1000);
        builder.Property(c => c.DesiredPosition).HasMaxLength(200);
        builder.Property(c => c.AiReview).HasColumnType("text");

        // Связь 1:1 с User объявлена в UserConfiguration.
        builder.HasIndex(c => c.UserId).IsUnique().HasDatabaseName("IX_CareerProfiles_UserId");

        builder.HasOne(c => c.ResumeFile).WithMany()
            .HasForeignKey(c => c.ResumeFileId).OnDelete(DeleteBehavior.NoAction);
    }
}
