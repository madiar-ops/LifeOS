using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Application.Interfaces.Repositories;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Repositories;

public class RefreshTokenRepository : GenericRepository<RefreshToken>, IRefreshTokenRepository
{
    private readonly IDateTimeProvider _dateTime;

    public RefreshTokenRepository(AppDbContext context, IDateTimeProvider dateTime) : base(context)
        => _dateTime = dateTime;

    /// <summary>С отслеживанием: найденный токен сразу помечается отозванным при ротации.</summary>
    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
        => await DbSet.FirstOrDefaultAsync(t => t.Token == token, cancellationToken);

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = _dateTime.UtcNow;

        var activeTokens = await DbSet
            .Where(t => t.UserId == userId && !t.IsRevoked && t.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            token.RevokedAt = now;
        }
        // SaveChanges вызывается на уровне UnitOfWork.
    }
}
