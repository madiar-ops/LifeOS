namespace LifeOS.IntegrationTests.Infrastructure;

/// <summary>
/// Объединяет все интеграционные тесты в одну коллекцию xUnit.
///
/// Классы внутри одной коллекции выполняются последовательно и делят общий
/// экземпляр <see cref="ApiFixture"/>. Это осознанный размен: параллельный
/// прогон потребовал бы отдельного контейнера PostgreSQL на класс тестов,
/// то есть десятков секунд на старт вместо нескольких.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFixture>
{
    public const string Name = "LifeOS API";
}
