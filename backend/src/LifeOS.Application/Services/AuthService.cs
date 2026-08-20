using LifeOS.Application.Common;
using LifeOS.Application.DTO.Auth;
using LifeOS.Application.Interfaces.Auth;
using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Application.Interfaces.Repositories;
using LifeOS.Domain.Entities;
using LifeOS.Domain.Enums;
using LifeOS.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LifeOS.Application.Services;

/// <summary>
/// Регистрация, вход, обновление и отзыв токенов.
/// Сервис не знает ни про HTTP, ни про EF Core — только про интерфейсы.
/// </summary>
public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;
    private readonly IDateTimeProvider _dateTime;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator,
        IDateTimeProvider dateTime,
        IOptions<JwtSettings> jwtSettings,
        ILogger<AuthService> logger)
    {
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _dateTime = dateTime;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);

        // Предварительная проверка даёт понятную ошибку 409 вместо падения
        // на уникальном индексе. Сам индекс остаётся последней линией защиты
        // от гонки двух одновременных регистраций.
        if (await _unitOfWork.Users.EmailExistsAsync(email, cancellationToken))
            throw new ConflictException(
                "Пользователь с таким email уже зарегистрирован.", "user.email_taken");

        var user = new User
        {
            Name = request.Name.Trim(),
            Surname = request.Surname.Trim(),
            Email = email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = UserRole.User
        };

        await _unitOfWork.Users.AddAsync(user, cancellationToken);

        var refreshToken = CreateRefreshToken(user.Id);
        await _unitOfWork.RefreshTokens.AddAsync(refreshToken, cancellationToken);

        // Пользователь и его первый refresh-токен сохраняются одним SaveChanges —
        // одна транзакция, промежуточного состояния не существует.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Зарегистрирован пользователь {UserId}", user.Id);

        return BuildAuthResponse(user, refreshToken.Token);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var user = await _unitOfWork.Users.GetByEmailAsync(email, cancellationToken);

        // Сообщение об ошибке ОДИНАКОВО для «нет такого email» и «неверный пароль».
        // Иначе форма логина превращается в инструмент перебора существующих аккаунтов.
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Неудачная попытка входа для {Email}", email);
            throw new BusinessRuleException("Неверный email или пароль.", "auth.invalid_credentials");
        }

        var refreshToken = CreateRefreshToken(user.Id);
        await _unitOfWork.RefreshTokens.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Вход выполнен: {UserId}", user.Id);

        return BuildAuthResponse(user, refreshToken.Token);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default)
    {
        var stored = await _unitOfWork.RefreshTokens.GetByTokenAsync(request.RefreshToken, cancellationToken)
            ?? throw new BusinessRuleException("Недействительный refresh-токен.", "auth.invalid_refresh_token");

        // Обнаружение повторного использования: если предъявлен уже отозванный токен,
        // значит его копия у кого-то ещё. Гасим ВСЮ цепочку токенов пользователя —
        // легитимный клиент вынужден будет войти заново, но злоумышленник теряет доступ.
        if (stored.IsRevoked)
        {
            _logger.LogWarning(
                "Повторное использование отозванного refresh-токена пользователя {UserId}. Отзываю все токены.",
                stored.UserId);

            await _unitOfWork.RefreshTokens.RevokeAllForUserAsync(stored.UserId, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            throw new BusinessRuleException(
                "Refresh-токен скомпрометирован. Войдите заново.", "auth.token_reuse_detected");
        }

        if (stored.IsExpiredAt(_dateTime.UtcNow))
            throw new BusinessRuleException("Срок действия refresh-токена истёк.", "auth.refresh_token_expired");

        var user = await _unitOfWork.Users.GetByIdAsync(stored.UserId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), stored.UserId);

        // Ротация: старый токен гасится и указывает на пришедший ему на смену.
        var newToken = CreateRefreshToken(user.Id);

        stored.IsRevoked = true;
        stored.RevokedAt = _dateTime.UtcNow;
        stored.ReplacedByToken = newToken.Token;

        await _unitOfWork.RefreshTokens.AddAsync(newToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return BuildAuthResponse(user, newToken.Token);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var stored = await _unitOfWork.RefreshTokens.GetByTokenAsync(refreshToken, cancellationToken);

        // Выход идемпотентен: неизвестный или уже погашенный токен — не ошибка.
        // Клиент в любом случае должен считать себя вышедшим.
        if (stored is null || stored.IsRevoked)
            return;

        stored.IsRevoked = true;
        stored.RevokedAt = _dateTime.UtcNow;

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Выход выполнен: {UserId}", stored.UserId);
    }

    public async Task<UserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);

        return MapToUserResponse(user);
    }

    // ---- Вспомогательные методы -----------------------------------------

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private RefreshToken CreateRefreshToken(Guid userId) => new()
    {
        UserId = userId,
        Token = _tokenGenerator.GenerateRefreshToken(),
        ExpiresAt = _dateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenDays)
    };

    private AuthResponse BuildAuthResponse(User user, string refreshToken)
    {
        var (accessToken, expiresAt) = _tokenGenerator.GenerateAccessToken(user);
        return new AuthResponse(accessToken, refreshToken, expiresAt, MapToUserResponse(user));
    }

    private static UserResponse MapToUserResponse(User user) => new(
        user.Id, user.Name, user.Surname, user.Email, user.AvatarUrl, user.Role, user.CreatedAt);
}
