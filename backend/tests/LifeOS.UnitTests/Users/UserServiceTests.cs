using FluentAssertions;
using LifeOS.Application.DTO.Users;
using LifeOS.Application.Interfaces.Auth;
using LifeOS.Application.Interfaces.Infrastructure;
using LifeOS.Application.Interfaces.Repositories;
using LifeOS.Application.Services;
using LifeOS.Domain.Entities;
using LifeOS.Domain.Enums;
using LifeOS.Domain.Exceptions;
using LifeOS.UnitTests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace LifeOS.UnitTests.Users;

/// <summary>
/// Тесты работы с профилем.
///
/// Главное здесь — не редактирование имени, а два правила безопасности:
/// нельзя прочитать чужой профиль и нельзя сменить пароль, не зная текущего.
/// </summary>
public class UserServiceTests
{
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();

    private readonly Guid _userId = Guid.NewGuid();
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _unitOfWork.Users.Returns(_users);
        _unitOfWork.RefreshTokens.Returns(_refreshTokens);
        _currentUser.GetRequiredUserId().Returns(_userId);
        _passwordHasher.Hash(Arg.Any<string>()).Returns(call => $"hash::{call.Arg<string>()}");

        _sut = new UserService(
            _unitOfWork,
            _currentUser,
            _passwordHasher,
            TestMapper.Create(),
            NullLogger<UserService>.Instance);
    }

    [Fact]
    public async Task Чужой_профиль_прочитать_нельзя()
    {
        var foreignId = Guid.NewGuid();

        await _sut.Invoking(s => s.GetByIdAsync(foreignId))
                  .Should().ThrowAsync<ForbiddenException>();

        // До репозитория запрос дойти не должен: проверка стоит раньше обращения к БД.
        await _users.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Свой_профиль_возвращается_без_хеша_пароля()
    {
        var user = CreateUser();
        _users.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(user);

        var response = await _sut.GetByIdAsync(_userId);

        response.Id.Should().Be(_userId);
        response.Email.Should().Be("dan@lifeos.kz");
        response.Role.Should().Be(UserRole.User);
    }

    [Fact]
    public async Task Имя_и_фамилия_обрезаются_от_пробелов()
    {
        var user = CreateUser();
        _users.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(user);

        await _sut.UpdateProfileAsync(new UpdateProfileRequest("  Данияр  ", "  Абубек  "));

        user.Name.Should().Be("Данияр");
        user.Surname.Should().Be("Абубек");
    }

    [Fact]
    public async Task Смена_пароля_без_знания_текущего_невозможна()
    {
        var user = CreateUser();
        _users.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("угадал?", user.PasswordHash).Returns(false);

        var act = () => _sut.ChangePasswordAsync(new ChangePasswordRequest("угадал?", "NewPassw0rd"));

        // Без этой проверки перехваченный access-токен позволял бы навсегда
        // захватить аккаунт: сменить пароль и выкинуть владельца.
        (await act.Should().ThrowAsync<BusinessRuleException>())
            .Which.Code.Should().Be("user.wrong_password");

        user.PasswordHash.Should().Be("hash::Passw0rd!", "пароль не должен меняться при неудачной проверке");
    }

    [Fact]
    public async Task Успешная_смена_пароля_завершает_все_сессии()
    {
        var user = CreateUser();
        _users.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(user);
        _passwordHasher.Verify("Passw0rd!", user.PasswordHash).Returns(true);

        await _sut.ChangePasswordAsync(new ChangePasswordRequest("Passw0rd!", "NewPassw0rd"));

        user.PasswordHash.Should().Be("hash::NewPassw0rd");

        // Смена пароля — стандартная реакция на подозрение о компрометации.
        // Оставить живыми старые refresh-токены значило бы оставить злоумышленнику доступ.
        await _refreshTokens.Received(1).RevokeAllForUserAsync(_userId, Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Профиль_удалённого_пользователя_даёт_404()
    {
        _users.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns((User?)null);

        await _sut.Invoking(s => s.UpdateProfileAsync(new UpdateProfileRequest("Дан", "Абубек")))
                  .Should().ThrowAsync<NotFoundException>();
    }

    private User CreateUser() => new()
    {
        Id = _userId,
        Name = "Дан",
        Surname = "Абубек",
        Email = "dan@lifeos.kz",
        PasswordHash = "hash::Passw0rd!",
        Role = UserRole.User
    };
}
