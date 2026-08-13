using LifeOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Data.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        // Класс TaskItem — таблица "Tasks" (имя класса изменено из-за конфликта
        // с System.Threading.Tasks.Task, схема БД от этого не страдает).
        builder.ToTable("Tasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Completed).IsRequired().HasDefaultValue(false);

        builder.HasOne(t => t.User).WithMany(u => u.Tasks)
            .HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);

        // Удаление цели НЕ удаляет задачи — GoalId становится NULL,
        // задача продолжает жить как самостоятельная.
        builder.HasOne(t => t.Goal).WithMany(g => g.Tasks)
            .HasForeignKey(t => t.GoalId).OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(t => t.UserId).HasDatabaseName("IX_Tasks_UserId");
        builder.HasIndex(t => t.GoalId).HasDatabaseName("IX_Tasks_GoalId");
        builder.HasIndex(t => new { t.UserId, t.Completed }).HasDatabaseName("IX_Tasks_UserId_Completed");
    }
}
