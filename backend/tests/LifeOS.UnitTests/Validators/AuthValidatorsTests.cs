using FluentAssertions;
using FluentValidation.TestHelper;
using LifeOS.Application.DTO.Auth;
using LifeOS.Application.DTO.Users;
using LifeOS.Application.Validators.Auth;
using LifeOS.Application.Validators.Users;

namespace LifeOS.UnitTests.Validators;

/// <summary>
/// Правила регистрации и смены пароля.
///
/// Эти же правила продублированы на фронтенде (zod-схемы). Тесты фиксируют
/// эталон: если правило меняется здесь, оно обязано измениться и там,
/// иначе форма пропустит данные, которые сервер отвергнет.
/// </summary>
public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _sut = new();

    // Второй параметр в отчёте о падении объясняет, ЧЕМ именно плох пароль.
    [Theory]
    [InlineData("short1A", "короче восьми символов")]
    [InlineData("passw0rd", "нет заглавной буквы")]
    [InlineData("PASSW0RD", "нет строчной буквы")]
    [InlineData("Password", "нет цифры")]
    public void Слабый_пароль_отклоняется(string password, string причина)
    {
        var result = _sut.TestValidate(Request(password: password));

        result.IsValid.Should().BeFalse($"пароль «{password}» недопустим: {причина}");
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("Passw0rd")]
    [InlineData("ОченьДлинный1Пароль")]
    [InlineData("A1bcdefg")]
    public void Пароль_с_буквами_обоих_регистров_и_цифрой_принимается(string password)
    {
        var result = _sut.TestValidate(Request(password: password));

        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Пароль_длиннее_128_символов_отклоняется()
    {
        // Верхняя граница нужна из-за BCrypt: он молча обрезает вход,
        // и без ограничения пользователь считал бы значимым весь свой пароль.
        var result = _sut.TestValidate(Request(password: "Aa1" + new string('x', 130)));

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("не-почта")]
    [InlineData("@lifeos.kz")]
    [InlineData("")]
    public void Некорректный_email_отклоняется(string email)
    {
        var result = _sut.TestValidate(Request(email: email));

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Пустые_имя_и_фамилия_отклоняются()
    {
        var result = _sut.TestValidate(new RegisterRequest("  ", "", "dan@lifeos.kz", "Passw0rd"));

        result.ShouldHaveValidationErrorFor(x => x.Name);
        result.ShouldHaveValidationErrorFor(x => x.Surname);
    }

    [Fact]
    public void Корректная_заявка_проходит_целиком()
    {
        var result = _sut.TestValidate(Request());

        result.IsValid.Should().BeTrue();
    }

    private static RegisterRequest Request(
        string name = "Данияр",
        string surname = "Абубек",
        string email = "dan@lifeos.kz",
        string password = "Passw0rd") => new(name, surname, email, password);
}

/// <summary>
/// Вход намеренно НЕ проверяет сложность пароля: иначе форма логина
/// подсказывала бы злоумышленнику правила формирования паролей в системе.
/// </summary>
public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _sut = new();

    [Fact]
    public void Простой_пароль_при_входе_не_вызывает_ошибки()
    {
        var result = _sut.TestValidate(new LoginRequest("dan@lifeos.kz", "123"));

        // Пароль мог быть задан до ужесточения правил — отказать во входе
        // из-за его слабости значило бы заблокировать существующий аккаунт.
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Пустой_пароль_отклоняется()
    {
        var result = _sut.TestValidate(new LoginRequest("dan@lifeos.kz", ""));

        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Некорректный_email_отклоняется()
    {
        var result = _sut.TestValidate(new LoginRequest("не-почта", "Passw0rd"));

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }
}

public class RefreshRequestValidatorTests
{
    private readonly RefreshRequestValidator _sut = new();

    [Fact]
    public void Пустой_токен_отклоняется()
        => _sut.TestValidate(new RefreshRequest("")).ShouldHaveValidationErrorFor(x => x.RefreshToken);

    [Fact]
    public void Слишком_длинный_токен_отклоняется()
    {
        // Настоящий токен — 64 случайных байта в Base64Url, это ~86 символов.
        // Значение в 200+ символов заведомо подделка; отсекаем до похода в БД.
        var result = _sut.TestValidate(new RefreshRequest(new string('t', 201)));

        result.ShouldHaveValidationErrorFor(x => x.RefreshToken);
    }

    [Fact]
    public void Токен_нормальной_длины_принимается()
        => _sut.TestValidate(new RefreshRequest(new string('t', 86)))
               .ShouldNotHaveValidationErrorFor(x => x.RefreshToken);
}

public class ChangePasswordRequestValidatorTests
{
    private readonly ChangePasswordRequestValidator _sut = new();

    [Fact]
    public void Новый_пароль_не_может_совпадать_с_текущим()
    {
        var result = _sut.TestValidate(new ChangePasswordRequest("Passw0rd", "Passw0rd"));

        result.ShouldHaveValidationErrorFor(x => x.NewPassword);
    }

    [Fact]
    public void Новый_пароль_проверяется_на_сложность()
    {
        var result = _sut.TestValidate(new ChangePasswordRequest("Passw0rd", "простой"));

        result.ShouldHaveValidationErrorFor(x => x.NewPassword);
    }

    [Fact]
    public void Текущий_пароль_обязателен()
    {
        var result = _sut.TestValidate(new ChangePasswordRequest("", "NewPassw0rd"));

        result.ShouldHaveValidationErrorFor(x => x.CurrentPassword);
    }

    [Fact]
    public void Корректная_смена_пароля_проходит()
        => _sut.TestValidate(new ChangePasswordRequest("Passw0rd", "NewPassw0rd"))
               .IsValid.Should().BeTrue();
}

public class UpdateProfileRequestValidatorTests
{
    private readonly UpdateProfileRequestValidator _sut = new();

    [Fact]
    public void Пустое_имя_отклоняется()
        => _sut.TestValidate(new UpdateProfileRequest("", "Абубек"))
               .ShouldHaveValidationErrorFor(x => x.Name);

    [Fact]
    public void Имя_длиннее_100_символов_отклоняется()
        => _sut.TestValidate(new UpdateProfileRequest(new string('и', 101), "Абубек"))
               .ShouldHaveValidationErrorFor(x => x.Name);

    [Fact]
    public void Обычные_имя_и_фамилия_принимаются()
        => _sut.TestValidate(new UpdateProfileRequest("Данияр", "Абубек"))
               .IsValid.Should().BeTrue();
}
