using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using LifeOS.Application.Common;
using LifeOS.Domain.Entities;
using LifeOS.Domain.Enums;
using LifeOS.Infrastructure.Auth;
using LifeOS.UnitTests.TestDoubles;
using Microsoft.Extensions.Options;

namespace LifeOS.UnitTests.Auth;

/// <summary>
/// Тесты хеширования паролей.
///
/// Проверяется не «работает ли BCrypt» (за это отвечает библиотека),
/// а два свойства, на которые опирается вся схема аутентификации:
/// хеш никогда не совпадает с паролем и не повторяется между вызовами.
/// </summary>
public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _sut = new();

    [Fact]
    public void Хеш_не_равен_исходному_паролю()
    {
        var hash = _sut.Hash("Passw0rd!");

        hash.Should().NotBe("Passw0rd!");
        hash.Should().NotContain("Passw0rd", "в хеше не должно остаться следа открытого пароля");
    }

    [Fact]
    public void Один_и_тот_же_пароль_даёт_разные_хеши()
    {
        var first = _sut.Hash("Passw0rd!");
        var second = _sut.Hash("Passw0rd!");

        // BCrypt подмешивает случайную соль. Без неё одинаковые пароли
        // разных пользователей были бы видны как одинаковые строки в БД,
        // и утечка таблицы сразу выдала бы, у кого пароль «123456».
        first.Should().NotBe(second);
    }

    [Fact]
    public void Оба_разных_хеша_проверяются_успешно()
    {
        var first = _sut.Hash("Passw0rd!");
        var second = _sut.Hash("Passw0rd!");

        _sut.Verify("Passw0rd!", first).Should().BeTrue();
        _sut.Verify("Passw0rd!", second).Should().BeTrue();
    }

    [Fact]
    public void Неверный_пароль_не_проходит_проверку()
        => _sut.Verify("другой", _sut.Hash("Passw0rd!")).Should().BeFalse();

    [Fact]
    public void Проверка_чувствительна_к_регистру()
        => _sut.Verify("passw0rd!", _sut.Hash("Passw0rd!")).Should().BeFalse();
}

/// <summary>
/// Тесты выдачи токенов.
///
/// Состав claim'ов — это контракт: <c>CurrentUserService</c> читает из токена
/// идентификатор пользователя, а <c>[Authorize(Roles = ...)]</c> — роль.
/// Потеря любого из них ломает авторизацию молча, без ошибки компиляции.
/// </summary>
public class JwtTokenGeneratorTests
{
    private readonly FixedDateTimeProvider _dateTime = FixedDateTimeProvider.Default;
    private readonly JwtSettings _settings = new()
    {
        Key = "unit-test-signing-key-at-least-32-chars",
        Issuer = "LifeOS.API",
        Audience = "LifeOS.Client",
        AccessTokenMinutes = 15,
        RefreshTokenDays = 7
    };

    private readonly JwtTokenGenerator _sut;
    private readonly User _user = new()
    {
        Name = "Дан",
        Surname = "Абубек",
        Email = "dan@lifeos.kz",
        PasswordHash = "hash",
        Role = UserRole.User
    };

    public JwtTokenGeneratorTests()
        => _sut = new JwtTokenGenerator(Options.Create(_settings), _dateTime);

    [Fact]
    public void Токен_содержит_идентификатор_email_и_роль()
    {
        var (token, _) = _sut.GenerateAccessToken(_user);

        var claims = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.ToList();

        claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == _user.Id.ToString());
        claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == "dan@lifeos.kz");

        // Роль записана как ClaimTypes.Role, но JwtSecurityTokenHandler при
        // выпуске токена сокращает длинные URI-имена по DefaultOutboundClaimTypeMap:
        // в самом JWT лежит короткое "role". Обратное отображение делает уже
        // JwtBearer при разборе, поэтому в CurrentUserService снова видно ClaimTypes.Role.
        // Тест принимает обе формы, чтобы не зависеть от версии библиотеки.
        claims.Should().Contain(
            c => (c.Type == ClaimTypes.Role || c.Type == "role") && c.Value == nameof(UserRole.User));
    }

    [Fact]
    public void Токен_не_содержит_хеш_пароля()
    {
        var user = new User
        {
            Name = "Дан",
            Surname = "Абубек",
            Email = "dan@lifeos.kz",
            PasswordHash = "$2a$12$секретныйХешПароля",
            Role = UserRole.User
        };

        var (token, _) = _sut.GenerateAccessToken(user);

        // JWT не шифруется, а лишь подписывается: его содержимое читает любой,
        // у кого есть строка токена. Проверять надо именно раскодированную
        // полезную нагрузку — в самой строке токена она лежит в Base64,
        // и поиск подстроки там ничего бы не доказал.
        var payload = string.Join(
            ";",
            new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.Select(c => $"{c.Type}={c.Value}"));

        payload.Should().NotContain(user.PasswordHash);
        payload.Should().NotContain("PasswordHash");
    }

    [Fact]
    public void Срок_жизни_токена_берётся_из_настроек()
    {
        var (_, expiresAt) = _sut.GenerateAccessToken(_user);

        expiresAt.Should().Be(_dateTime.UtcNow.AddMinutes(15));
    }

    [Fact]
    public void Издатель_и_аудитория_совпадают_с_настройками_проверки()
    {
        var (token, _) = _sut.GenerateAccessToken(_user);
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // Именно эти значения проверяет AuthenticationExtensions.
        // Расхождение дало бы 401 на каждом запросе с валидным токеном.
        parsed.Issuer.Should().Be("LifeOS.API");
        parsed.Audiences.Should().Contain("LifeOS.Client");
    }

    [Fact]
    public void Каждый_токен_имеет_собственный_jti()
    {
        var first = ReadJti(_sut.GenerateAccessToken(_user).Token);
        var second = ReadJti(_sut.GenerateAccessToken(_user).Token);

        first.Should().NotBe(second);
    }

    [Fact]
    public void Refresh_токен_не_является_JWT()
    {
        var refresh = _sut.GenerateRefreshToken();

        // Подделать refresh-токен невозможно в принципе именно потому, что он
        // не самоописывающийся: его валидность проверяется только записью в БД.
        refresh.Should().NotContain(".");
        new JwtSecurityTokenHandler().CanReadToken(refresh).Should().BeFalse();
    }

    [Fact]
    public void Refresh_токены_не_повторяются()
    {
        var tokens = Enumerable.Range(0, 200).Select(_ => _sut.GenerateRefreshToken()).ToList();

        tokens.Should().OnlyHaveUniqueItems();
        tokens.Should().OnlyContain(t => t.Length >= 64, "64 случайных байта в Base64Url — это ~86 символов");
    }

    private static string ReadJti(string token)
        => new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
            .Single(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
}
