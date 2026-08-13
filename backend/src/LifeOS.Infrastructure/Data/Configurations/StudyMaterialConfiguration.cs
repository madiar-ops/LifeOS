using LifeOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Data.Configurations;

public class StudyMaterialConfiguration : IEntityTypeConfiguration<StudyMaterial>
{
    public void Configure(EntityTypeBuilder<StudyMaterial> builder)
    {
        builder.ToTable("StudyMaterials");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Title).IsRequired().HasMaxLength(200);

        // Summary может быть длинным текстом от LLM — снимаем ограничение из конвенции.
        builder.Property(m => m.Summary).HasColumnType("text");

        builder.HasOne(m => m.User).WithMany(u => u.StudyMaterials)
            .HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);

        // NoAction (а не Restrict): файл нельзя удалить, пока на него ссылается материал,
        // НО при каскадном удалении пользователя проверка откладывается до конца
        // оператора — иначе Postgres упал бы на конфликте двух каскадных путей.
        builder.HasOne(m => m.File).WithMany()
            .HasForeignKey(m => m.FileId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(m => m.UserId).HasDatabaseName("IX_StudyMaterials_UserId");
        builder.HasIndex(m => m.FileId).HasDatabaseName("IX_StudyMaterials_FileId");
    }
}
