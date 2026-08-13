using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.API.Extensions;

public static class MigrationExtensions
{
    /// <summary>
    /// Применяет ожидающие миграции при старте.
    /// Включается только в Development: в проде миграции накатываются
    /// отдельным шагом деплоя, иначе несколько инстансов гонятся за схему.
    /// </summary>
    public static async Task ApplyMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
            if (pending.Count == 0)
            {
                logger.LogInformation("Схема БД актуальна, миграций к применению нет.");
                return;
            }

            logger.LogInformation("Применяю миграции: {Migrations}", string.Join(", ", pending));
            await context.Database.MigrateAsync();
            logger.LogInformation("Миграции успешно применены.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Не удалось применить миграции.");
            throw;
        }
    }
}
