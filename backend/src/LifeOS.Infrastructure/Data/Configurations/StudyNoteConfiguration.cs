using LifeOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Data.Configurations;

public class StudyNoteConfiguration : IEntityTypeConfiguration<StudyNote>
{
    public void Configure(EntityTypeBuilder<StudyNote> builder)
    {
        builder.ToTable("StudyNotes");
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Content).IsRequired().HasColumnType("text");

        builder.HasOne(n => n.StudyMaterial).WithMany(m => m.Notes)
            .HasForeignKey(n => n.StudyMaterialId).OnDelete(DeleteBehavior.Cascade);

        // Владелец заметки дублируется для быстрой фильтрации и проверки прав
        // без JOIN к StudyMaterials. NoAction — чтобы избежать двойного каскада.
        builder.HasOne(n => n.User).WithMany(u => u.StudyNotes)
            .HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(n => n.StudyMaterialId).HasDatabaseName("IX_StudyNotes_StudyMaterialId");
        builder.HasIndex(n => n.UserId).HasDatabaseName("IX_StudyNotes_UserId");
    }
}
