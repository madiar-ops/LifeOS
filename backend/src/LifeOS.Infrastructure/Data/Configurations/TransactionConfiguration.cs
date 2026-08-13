using LifeOS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LifeOS.Infrastructure.Data.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Type).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Category).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Amount).IsRequired().HasPrecision(18, 2);
        builder.Property(t => t.Currency).IsRequired().HasMaxLength(3).IsFixedLength();
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.Property(t => t.Date).IsRequired();

        builder.HasOne(t => t.User).WithMany(u => u.Transactions)
            .HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);

        // Составной индекс: Dashboard считает баланс за период по (UserId, Date).
        builder.HasIndex(t => new { t.UserId, t.Date }).HasDatabaseName("IX_Transactions_UserId_Date");
        builder.HasIndex(t => new { t.UserId, t.Type }).HasDatabaseName("IX_Transactions_UserId_Type");
    }
}
