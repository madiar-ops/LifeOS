using AutoMapper;
using LifeOS.Application.Mappings;

namespace LifeOS.UnitTests.TestDoubles;

/// <summary>
/// Настоящий AutoMapper с боевым профилем проекта.
///
/// Подставлять сюда заглушку было бы ошибкой: половина ценности теста сервиса
/// в том, что сущность действительно превращается в DTO с правильными полями.
/// Заглушка вернула бы то, что ей велели, и проверяла бы сама себя.
/// </summary>
public static class TestMapper
{
    private static readonly MapperConfiguration Configuration =
        new(cfg => cfg.AddProfile<MappingProfile>());

    public static IMapper Create() => Configuration.CreateMapper();
}
