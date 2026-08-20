using FluentAssertions;
using LifeOS.Application.Common;

namespace LifeOS.UnitTests.Common;

/// <summary>
/// Тесты пагинации.
///
/// Верхняя граница страницы — это не удобство, а защита: без неё запрос
/// `?pageSize=1000000` заставил бы сервер выгрузить всю таблицу в память.
/// </summary>
public class PaginationParamsTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    public void Номер_страницы_не_опускается_ниже_первой(int input, int expected)
    {
        var pagination = new PaginationParams { PageNumber = input };

        pagination.PageNumber.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-10, 1)]
    [InlineData(20, 20)]
    [InlineData(100, 100)]
    [InlineData(101, 100)]
    [InlineData(int.MaxValue, 100)]
    public void Размер_страницы_зажимается_в_диапазон_1_100(int input, int expected)
    {
        var pagination = new PaginationParams { PageSize = input };

        // Значение молча приводится к допустимому, а не отклоняется ошибкой:
        // клиент, попросивший слишком много, получает максимум, а не 400.
        pagination.PageSize.Should().Be(expected);
    }

    [Fact]
    public void По_умолчанию_первая_страница_по_двадцать_записей()
    {
        var pagination = new PaginationParams();

        pagination.PageNumber.Should().Be(1);
        pagination.PageSize.Should().Be(20);
        pagination.Skip.Should().Be(0);
    }

    [Fact]
    public void Skip_считается_от_номера_страницы_и_её_размера()
    {
        var pagination = new PaginationParams { PageNumber = 4, PageSize = 25 };

        pagination.Skip.Should().Be(75);
    }
}

/// <summary>
/// Метаданные страницы. Фронтенд не вычисляет наличие соседних страниц сам —
/// он доверяет флагам с сервера, поэтому флаги обязаны быть верны на границах.
/// </summary>
public class PagedResultTests
{
    [Fact]
    public void Число_страниц_округляется_вверх()
    {
        var page = new PagedResult<int>(new[] { 1, 2, 3 }, totalCount: 21, pageNumber: 1, pageSize: 20);

        // 21 запись при размере 20 — это две страницы, а не одна.
        page.TotalPages.Should().Be(2);
    }

    [Fact]
    public void На_первой_странице_нет_предыдущей_а_следующая_есть()
    {
        var page = new PagedResult<int>(new[] { 1 }, totalCount: 50, pageNumber: 1, pageSize: 20);

        page.HasPrevious.Should().BeFalse();
        page.HasNext.Should().BeTrue();
    }

    [Fact]
    public void На_последней_странице_следующей_нет()
    {
        var page = new PagedResult<int>(new[] { 1 }, totalCount: 50, pageNumber: 3, pageSize: 20);

        page.HasPrevious.Should().BeTrue();
        page.HasNext.Should().BeFalse();
    }

    [Fact]
    public void Пустая_страница_не_обещает_следующую()
    {
        var page = PagedResult<int>.Empty(pageNumber: 1, pageSize: 20);

        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
        page.TotalPages.Should().Be(0);
        page.HasPrevious.Should().BeFalse();
        page.HasNext.Should().BeFalse();
    }

    [Fact]
    public void Нулевой_размер_страницы_не_приводит_к_делению_на_ноль()
    {
        var page = new PagedResult<int>(Array.Empty<int>(), totalCount: 10, pageNumber: 1, pageSize: 0);

        page.TotalPages.Should().Be(0);
    }

    [Fact]
    public void Преобразование_в_DTO_сохраняет_метаданные_страницы()
    {
        var source = new PagedResult<int>(new[] { 1, 2, 3 }, totalCount: 42, pageNumber: 2, pageSize: 20);

        var mapped = source.Map(number => number.ToString());

        mapped.Items.Should().Equal("1", "2", "3");
        mapped.TotalCount.Should().Be(42);
        mapped.PageNumber.Should().Be(2);
        mapped.PageSize.Should().Be(20);
        mapped.HasNext.Should().BeTrue();
    }
}
