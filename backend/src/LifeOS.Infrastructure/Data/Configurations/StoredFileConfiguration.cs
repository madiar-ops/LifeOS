using LifeOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Data.Configurations;

public class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.ToTable("Files");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.FileName).IsRequired().HasMaxLength(255);
        builder.Property(f => f.FirebaseUrl).IsRequired().HasMaxLength(1000);
        builder.Property(f => f.StoragePath).IsRequired().HasMaxLength(1000);
        builder.Property(f => f.ContentType).IsRequired().HasMaxLength(100);
        builder.Property(f => f.SizeBytes).IsRequired();
        builder.Property(f => f.Module).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.HasOne(f => f.User).WithMany(u => u.Files)
            .HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => new { f.UserId, f.Module }).HasDatabaseName("IX_Files_UserId_Module");
    }
}
