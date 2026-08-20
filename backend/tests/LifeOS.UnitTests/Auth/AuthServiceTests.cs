using FluentAssertions;
using LifeOS.Application.Common;
using LifeOS.Application.DTO.Auth;
using LifeOS.Application.Interfaces.Auth;
using LifeOS.Application.Interfaces.Repositories;
using LifeOS.Application.Services;
using LifeOS.Domain.Entities;
using LifeOS.Domain.Enums;
using LifeOS.Domain.Exceptions;
using LifeOS.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace LifeOS.UnitTests.Auth;

/// <summary>
/// Тесты аутентификации.
///
/// Здесь проверяется не «работает ли вход», а то, ради чего писалась
/// Фаза 2: одинаковый ответ на неверный логин и неверный пароль, ротация
/// refresh-токенов и реакция на предъявление уже отозванного токена.
/// Ошибка в любом из этих мест — это дыра в безопасности, а не неудобство.
/// </summary>
public class AuthServiceTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenGenerator _tokenGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly FixedDateTimeProvider _dateTime = FixedDateTimeProvider.Default;

    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _unitOfWork.Users.Returns(_users);
        _unitOfWork.RefreshTokens.Returns(_refreshTokens);

        _passwordHasher.Hash(Arg.Any<string>()).Returns(call => $"hash::{call.Arg<string>()}");
        _tokenGenerator.GenerateRefreshToken().Returns(_ => Guid.NewGuid().ToString("N"));
        _tokenGenerator
            .GenerateAccessToken(Arg.Any<User>())
            .Returns(_ => ("access-token", _dateTime.UtcNow.AddMinutes(15)));

        _sut = new AuthService(
            _unitOfWork,
            _passwordHasher,
            _tokenGenerator,
            _dateTime,
            Options.Create(new JwtSettings
            {
                Key = "unit-test-signing-key-at-least-32-chars",
                AccessTokenMinutes = 15,
                RefreshTokenDays = 7
            }),
            NullLogger<AuthService>.Instance);
    }

    // ---- Регистрация -----------------------------------------------------

    [Fact]
    public async Task Регистрация_с_занятым_email_отклоняется_кодом_user_email_taken()
    {
        _users.EmailExistsAsync("taken@lifeos.kz", Arg.Any<CancellationToken>()).Returns(true);

        var act = () => _sut.RegisterAsync(
            new RegisterRequest("Данияр", "Абубек", "taken@lifeos.kz", "Passw0rd!"));

        var exception = await act.Should().ThrowAsync<ConflictException>();
        exception.Which.Code.Should().Be("user.email_taken");

        // Ни пользователь, ни токен не должны были сохраниться.
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("  Dan@LifeOS.KZ  ")]
    [InlineData("DAN@LIFEOS.KZ")]
    public async Task Email_приводится_к_нижнему_регистру_и_обрезается(string raw)
    {
        User? saved = null;
        _users.When(r => r.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()))
              .Do(call => saved = call.Arg<User>());

        await _sut.RegisterAsync(new RegisterRequest("Дан", "Абубек", raw, "Passw0rd!"));

        // Уникальный индекс по email не различает регистр только потому,
        // что значение нормализуется здесь. Проверка на существование email
        // и последующий поиск при входе обязаны работать с одной и той же формой.
        saved.Should().NotBeNull();
        saved!.Email.Should().Be("dan@lifeos.kz");
    }

    [Fact]
    public async Task Новый_пользователь_получает_роль_User_и_только_хеш_пароля()
    {
        User? saved = null;
        _users.When(r => r.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>()))
              .Do(call => saved = call.Arg<User>());

        await _sut.RegisterAsync(new RegisterRequest("Дан", "Абубек", "dan@lifeos.kz", "Passw0rd!"));

        saved!.Role.Should().Be(UserRole.User, "роль администратора нельзя получить самостоятельной регистрацией");
        saved.PasswordHash.Should().Be("hash::Passw0rd!");
        saved.PasswordHash.Should().NotContain("Passw0rd!", "открытый пароль не должен доходить до хранилища");
    }

    [Fact]
    public async Task Пользователь_и_его_первый_refresh_токен_сохраняются_одним_SaveChanges()
    {
        await _sut.RegisterAsync(new RegisterRequest("Дан", "Абубек", "dan@lifeos.kz", "Passw0rd!"));

        // Промежуточного состояния «пользователь есть, войти нельзя» существовать не должно:
        // обе записи фиксируются одной транзакцией.
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _users.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _refreshTokens.Received(1).AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Срок_жизни_refresh_токена_берётся_из_настроек_а_не_из_системного_времени()
    {
        RefreshToken? saved = null;
        _refreshTokens.When(r => r.AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>()))
                      .Do(call => saved = call.Arg<RefreshToken>());

        await _sut.RegisterAsync(new RegisterRequest("Дан", "Абубек", "dan@lifeos.kz", "Passw0rd!"));

        saved!.ExpiresAt.Should().Be(_dateTime.UtcNow.AddDays(7));
    }

    // ---- Вход ------------------------------------------------------------

    [Fact]
    public async Task Несуществующий_email_и_неверный_пароль_дают_ОДИНАКОВУЮ_ошибку()
    {
        // Неизвестный email.
        _users.GetByEmailAsync("ghost@lifeos.kz", Arg.Any<CancellationToken>()).Returns((User?)null);

        var unknownEmail = await Record.ExceptionAsync(
            () => _sut.LoginAsync(new LoginRequest("ghost@lifeos.kz", "Passw0rd!")));

        // Существующий email, но неверный пароль.
        var user = CreateUser();
        _users.GetByEmailAsync("dan@lifeos.kz", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("wrong", user.PasswordHash).Returns(false);

        var wrongPassword = await Record.ExceptionAsync(
            () => _sut.LoginAsync(new LoginRequest("dan@lifeos.kz", "wrong")));

        // Различие в тексте или коде ошибки превратило бы форму входа
        // в инструмент проверки «есть ли такой пользователь».
        unknownEmail.Should().BeOfType<BusinessRuleException>();
        wrongPassword.Should().BeOfType<BusinessRuleException>();
        unknownEmail!.Message.Should().Be(wrongPassword!.Message);
        ((BusinessRuleException)unknownEmail).Code
            .Should().Be(((BusinessRuleException)wrongPassword).Code)
            .And.Be("auth.invalid_credentials");
    }

    [Fact]
    public async Task Успешный_вход_выдаёт_пару_токенов_и_профиль_без_хеша_пароля()
    {
        var user = CreateUser();
        _users.GetByEmailAsync("dan@lifeos.kz", Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("Passw0rd!", user.PasswordHash).Returns(true);

        var response = await _sut.LoginAsync(new LoginRequest("dan@lifeos.kz", "Passw0rd!"));

        response.AccessToken.Should().Be("access-token");
        response.RefreshToken.Should().NotBeNullOrWhiteSpace();
        response.AccessTokenExpiresAt.Should().Be(_dateTime.UtcNow.AddMinutes(15));
        response.User.Id.Should().Be(user.Id);
        response.User.Email.Should().Be("dan@lifeos.kz");

        // UserResponse физически не содержит поля с хешем — это гарантия типа,
        // а не договорённость. Тест фиксирует её, чтобы поле не добавили позже.
        typeof(UserResponse).GetProperty("PasswordHash").Should().BeNull();
    }

    // ---- Обновление токена ----------------------------------------------

    [Fact]
    public async Task Неизвестный_refresh_токен_отклоняется()
    {
        _refreshTokens.GetByTokenAsync("нет-такого", Arg.Any<CancellationToken>())
                      .Returns((RefreshToken?)null);

        var act = () => _sut.RefreshAsync(new RefreshRequest("нет-такого"));

        (await act.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be("auth.invalid_refresh_token");
    }

    [Fact]
    public async Task Предъявление_отозванного_токена_гасит_ВСЮ_цепочку_токенов_пользователя()
    {
        var user = CreateUser();
        var revoked = new RefreshToken
        {
            UserId = user.Id,
            Token = "украденный",
            ExpiresAt = _dateTime.UtcNow.AddDays(5),
            IsRevoked = true
        };
        _refreshTokens.GetByTokenAsync("украденный", Arg.Any<CancellationToken>()).Returns(revoked);

        var act = () => _sut.RefreshAsync(new RefreshRequest("украденный"));

        (await act.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be("auth.token_reuse_detected");

        // Отозванный токен предъявили — значит его копия есть у кого-то ещё.
        // Единственная безопасная реакция: погасить все токены пользователя,
        // даже ценой принудительного повторного входа легитимного клиента.
        await _refreshTokens.Received(1).RevokeAllForUserAsync(user.Id, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Истёкший_refresh_токен_отклоняется_отдельным_кодом()
    {
        var stored = new RefreshToken
        {
            UserId = Guid.NewGuid(),
            Token = "старый",
            // IsExpired внутри сущности сравнивается с реальным DateTime.UtcNow,
            // поэтому дата берётся заведомо в прошлом относительно любого запуска.
            ExpiresAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        _refreshTokens.GetByTokenAsync("старый", Arg.Any<CancellationToken>()).Returns(stored);

        var act = () => _sut.RefreshAsync(new RefreshRequest("старый"));

        // Отдельный код нужен фронтенду: истёкший токен — повод показать форму
        // входа, а скомпрометированный — повод показать предупреждение.
        (await act.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be("auth.refresh_token_expired");
    }

    [Fact]
    public async Task Обновление_ротирует_токен_и_связывает_старый_с_новым()
    {
        var user = CreateUser();
        var stored = new RefreshToken
        {
            UserId = user.Id,
            Token = "действующий",
            ExpiresAt = _dateTime.UtcNow.AddDays(5)
        };

        _refreshTokens.GetByTokenAsync("действующий", Arg.Any<CancellationToken>()).Returns(stored);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        RefreshToken? issued = null;
        _refreshTokens.When(r => r.AddAsync(Arg.Any<RefreshToken>(), Arg.Any<CancellationToken>()))
                      .Do(call => issued = call.Arg<RefreshToken>());

        var response = await _sut.RefreshAsync(new RefreshRequest("действующий"));

        // Старый токен становится непригоден сразу — именно это и позволяет
        // обнаружить кражу при его повторном предъявлении.
        stored.IsRevoked.Should().BeTrue();
        stored.RevokedAt.Should().Be(_dateTime.UtcNow);
        stored.ReplacedByToken.Should().Be(issued!.Token);

        response.RefreshToken.Should().Be(issued.Token);
        response.RefreshToken.Should().NotBe("действующий");
    }

    // ---- Выход -----------------------------------------------------------

    [Fact]
    public async Task Выход_с_неизвестным_токеном_не_считается_ошибкой()
    {
        _refreshTokens.GetByTokenAsync("чужой", Arg.Any<CancellationToken>()).Returns((RefreshToken?)null);

        var act = () => _sut.LogoutAsync("чужой");

        // Клиент в любом случае обязан считать себя вышедшим; исключение здесь
        // заставило бы фронтенд показывать ошибку на успешном по смыслу действии.
        await act.Should().NotThrowAsync();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Повторный_выход_не_переписывает_дату_отзыва()
    {
        var revokedAt = _dateTime.UtcNow.AddHours(-3);
        var stored = new RefreshToken
        {
            UserId = Guid.NewGuid(),
            Token = "уже-погашен",
            ExpiresAt = _dateTime.UtcNow.AddDays(5),
            IsRevoked = true,
            RevokedAt = revokedAt
        };
        _refreshTokens.GetByTokenAsync("уже-погашен", Arg.Any<CancellationToken>()).Returns(stored);

        await _sut.LogoutAsync("уже-погашен");

        stored.RevokedAt.Should().Be(revokedAt, "момент отзыва — факт, а не текущее время последнего запроса");
    }

    [Fact]
    public async Task Выход_гасит_действующий_токен()
    {
        var stored = new RefreshToken
        {
            UserId = Guid.NewGuid(),
            Token = "действующий",
            ExpiresAt = _dateTime.UtcNow.AddDays(5)
        };
        _refreshTokens.GetByTokenAsync("действующий", Arg.Any<CancellationToken>()).Returns(stored);

        await _sut.LogoutAsync("действующий");

        stored.IsRevoked.Should().BeTrue();
        stored.RevokedAt.Should().Be(_dateTime.UtcNow);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ---- Текущий пользователь -------------------------------------------

    [Fact]
    public async Task Запрос_профиля_несуществующего_пользователя_даёт_404()
    {
        var id = Guid.NewGuid();
        _users.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((User?)null);

        await _sut.Invoking(s => s.GetCurrentUserAsync(id))
                  .Should().ThrowAsync<NotFoundException>();
    }

    // ---- Вспомогательное -------------------------------------------------

    private static User CreateUser() => new()
    {
        Name = "Дан",
        Surname = "Абубек",
        Email = "dan@lifeos.kz",
        PasswordHash = "hash::Passw0rd!",
        Role = UserRole.User
    };
}
