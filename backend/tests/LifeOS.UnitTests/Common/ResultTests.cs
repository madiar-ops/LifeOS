using FluentAssertions;
using LifeOS.Application.Common;

namespace LifeOS.UnitTests.Common;

/// <summary>
/// Тесты типа <see cref="Result"/>.
///
/// Смысл типа — сделать невозможными состояния «успех с ошибкой» и
/// «неуспех без ошибки». Тесты фиксируют именно эту невозможность,
/// а не то, что свойства возвращают присвоенные значения.
/// </summary>
public class ResultTests
{
    [Fact]
    public void Успешный_результат_не_содержит_ошибки()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Неуспешный_результат_несёт_код_и_сообщение()
    {
        var result = Result.Failure(Error.NotFound("Цель не найдена."));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("resource.not_found");
        result.Error.Message.Should().Be("Цель не найдена.");
    }

    [Fact]
    public void Значение_неуспешного_результата_прочитать_нельзя()
    {
        var result = Result.Failure<string>(Error.Validation("Некорректные данные."));

        // Чтение Value у неуспеха — это ошибка программиста, а не пользователя,
        // поэтому исключение здесь уместнее молчаливого null.
        result.Invoking(r => r.Value).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Значение_успешного_результата_доступно()
    {
        var result = Result.Success("готово");

        result.Value.Should().Be("готово");
    }

    [Fact]
    public void Значение_неявно_превращается_в_успешный_результат()
    {
        Result<int> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Theory]
    [InlineData("resource.not_found")]
    [InlineData("resource.conflict")]
    [InlineData("validation.failed")]
    [InlineData("access.forbidden")]
    public void Коды_ошибок_машиночитаемы_и_стабильны(string expectedCode)
    {
        // Фронтенд разбирает именно эти строки (types/errors.ts), поэтому их
        // переименование — ломающее изменение контракта, а не рефакторинг.
        var errors = new[]
        {
            Error.NotFound("x"),
            Error.Conflict("x"),
            Error.Validation("x"),
            Error.Forbidden("x")
        };

        errors.Select(e => e.Code).Should().Contain(expectedCode);
    }

    [Fact]
    public void Пустая_ошибка_равна_самой_себе_по_значению()
    {
        // Error — record, и сравнение по значению нужно самому Result:
        // конструктор проверяет `error != Error.None`.
        new Error(string.Empty, string.Empty).Should().Be(Error.None);
    }
}
