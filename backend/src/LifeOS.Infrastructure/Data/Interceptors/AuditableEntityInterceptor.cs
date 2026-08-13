using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LifeOS.Infrastructure.Data.Interceptors;

/// <summary>
/// Автоматически проставляет CreatedAt / UpdatedAt при сохранении.
/// Благодаря этому ни один сервис не обязан помнить про аудит-поля,
/// и они физически не могут быть подделаны из DTO.
/// </summary>
public sealed class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly IDateTimeProvider _dateTime;

    public AuditableEntityInterceptor(IDateTimeProvider dateTime) => _dateTime = dateTime;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAudit(DbContext? context)
    {
        if (context is null) return;

        var now = _dateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;

                if (entry.Entity is IAuditableEntity auditableOnAdd)
                    auditableOnAdd.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified && entry.Entity is IAuditableEntity auditable)
            {
                // CreatedAt защищаем от перезаписи даже при подмене из вне.
                entry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;
                auditable.UpdatedAt = now;
            }
        }
    }
}
