using LifeOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Data.Configurations;

public class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> builder)
    {
        builder.ToTable("Quizzes");
        builder.HasKey(q => q.Id);

        // jsonb, а не text: PostgreSQL валидирует структуру и умеет по ней искать.
        builder.Property(q => q.Questions).IsRequired().HasColumnType("jsonb");
        builder.Property(q => q.TotalQuestions).IsRequired();

        builder.HasOne(q => q.StudyMaterial).WithMany(m => m.Quizzes)
            .HasForeignKey(q => q.StudyMaterialId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(q => q.User).WithMany(u => u.Quizzes)
            .HasForeignKey(q => q.UserId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(q => q.StudyMaterialId).HasDatabaseName("IX_Quizzes_StudyMaterialId");
        builder.HasIndex(q => q.UserId).HasDatabaseName("IX_Quizzes_UserId");
    }
}
