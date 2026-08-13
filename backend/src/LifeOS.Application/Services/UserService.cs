using AutoMapper;
using LifeOS.Application.DTO.Auth;
using LifeOS.Application.DTO.Users;
using LifeOS.Application.Interfaces.Auth;
using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Application.Interfaces.Repositories;
using LifeOS.Application.Interfaces.Services;
using LifeOS.Domain.Entities;
using LifeOS.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace LifeOS.Application.Services;

public class UserService : IUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IMapper _mapper;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher,
        IMapper mapper,
        ILogger<UserService> logger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _mapper = mapper;
        _logger = logger;
    }

    /// <summary>
    /// Профиль по Id. Пользователь может смотреть только себя;
    /// просмотр чужих профилей — задача админского модуля (Фаза 13).
    /// </summary>
    public async Task<UserResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (id != _currentUser.GetRequiredUserId())
            throw new ForbiddenException("Просмотр чужого профиля недоступен.");

        var user = await _unitOfWork.Users.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(User), id);

        return _mapper.Map<UserResponse>(user);
    }

    public async Task<UserResponse> UpdateProfileAsync(
        UpdateProfileRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        user.Name = request.Name.Trim();
        user.Surname = request.Surname.Trim();

        // UpdatedAt проставит AuditableEntityInterceptor — вручную не трогаем.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return _mapper.Map<UserResponse>(user);
    }

    public async Task ChangePasswordAsync(
        ChangePasswordRequest request, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        // Текущий пароль обязателен: иначе перехваченный access-токен
        // позволял бы навсегда захватить аккаунт, сменив пароль.
        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            throw new BusinessRuleException("Текущий пароль указан неверно.", "user.wrong_password");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);

        // После смены пароля все сессии должны завершиться —
        // это стандартная реакция на возможную компрометацию.
        await _unitOfWork.RefreshTokens.RevokeAllForUserAsync(userId, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Пароль изменён, все сессии отозваны: {UserId}", userId);
    }
}
