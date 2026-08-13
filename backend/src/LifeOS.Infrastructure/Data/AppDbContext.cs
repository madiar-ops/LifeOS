using System.Reflection;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data.Converters;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Data;

/// <summary>
/// Контекст EF Core — единственный владелец схемы PostgreSQL.
/// Никакой другой сервис (включая FastAPI) к БД не подключается.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<HealthLog> HealthLogs => Set<HealthLog>();
    public DbSet<StudyMaterial> StudyMaterials => Set<StudyMaterial>();
    public DbSet<StudyNote> StudyNotes => Set<StudyNote>();
    public DbSet<Quiz> Quizzes => Set<Quiz>();
    public DbSet<CareerProfile> CareerProfiles => Set<CareerProfile>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<AiHistoryEntry> AiHistory => Set<AiHistoryEntry>();
    public DbSet<StoredFile> Files => Set<StoredFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Подхватывает все IEntityTypeConfiguration из папки Data/Configurations.
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(modelBuilder);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Единая точность для всех денежных и дробных значений — чтобы EF не ругался
        // предупреждениями и в БД не появлялись разные numeric-типы.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);

        // Строки по умолчанию ограничены — защищает от неограниченного text везде подряд.
        configurationBuilder.Properties<string>().HaveMaxLength(500);

        // Все DateTime уходят в PostgreSQL как UTC. Без этого Npgsql бросает
        // исключение на любой дате с Kind = Unspecified, пришедшей из JSON-запроса.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcNullableDateTimeConverter>();
    }
}
