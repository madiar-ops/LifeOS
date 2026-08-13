using LifeOS.Application.Interfaces.Repositories;
using LifeOS.Domain.Entities;
using LifeOS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LifeOS.Infrastructure.Repositories;

public class UserRepository : GenericRepository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    /// <summary>
    /// Возвращает пользователя с отслеживанием (не AsNoTracking): результат
    /// используется при логине и обновлении профиля, где сущность меняется.
    /// </summary>
    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return await DbSet.FirstOrDefaultAsync(u => u.Email == normalized, cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return await DbSet.AnyAsync(u => u.Email == normalized, cancellationToken);
    }

    public async Task<User?> GetWithRefreshTokensAsync(Guid userId, CancellationToken cancellationToken = default)
        => await DbSet
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
}
