using AutoMapper;
using FluentAssertions;
using LifeOS.Application.DTO.Auth;
using LifeOS.Application.DTO.Files;
using LifeOS.Application.DTO.Goals;
using LifeOS.Application.DTO.Tasks;
using LifeOS.Application.Mappings;
using LifeOS.Domain.Entities;
using LifeOS.Domain.Enums;
using LifeOS.UnitTests.TestDoubles;

namespace LifeOS.UnitTests.Mappings;

/// <summary>
/// Тесты профиля AutoMapper.
///
/// Маппинг — самое незаметное место для ошибки: перепутанные поля не ломают
/// компиляцию и проявляются только на экране пользователя. Отдельно
/// проверяется, что наружу не уходят внутренние поля хранилища.
/// </summary>
public class MappingProfileTests
{
    private readonly IMapper _mapper = TestMapper.Create();

    [Fact]
    public void Конфигурация_маппинга_корректна()
    {
        var configuration = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());

        // Ловит несопоставленные параметры конструктора DTO — именно так
        // проявляется забытое поле после расширения ответа.
        configuration.Invoking(c => c.AssertConfigurationIsValid()).Should().NotThrow();
    }

    [Fact]
    public void Счётчики_задач_цели_считаются_по_вложенной_коллекции()
    {
        var goal = new Goal
        {
            UserId = Guid.NewGuid(),
            Title = "Защитить диплом",
            Status = GoalStatus.InProgress,
            Priority = PriorityLevel.High,
            Tasks =
            {
                new TaskItem { Title = "Написать backend", Completed = true },
                new TaskItem { Title = "Написать frontend", Completed = true },
                new TaskItem { Title = "Написать тесты", Completed = false }
            }
        };

        var response = _mapper.Map<GoalResponse>(goal);

        response.TotalTasks.Should().Be(3);
        response.CompletedTasks.Should().Be(2);
    }

    [Fact]
    public void Цель_без_задач_даёт_нули_а_не_исключение()
    {
        var goal = new Goal { UserId = Guid.NewGuid(), Title = "Новая цель" };

        var response = _mapper.Map<GoalResponse>(goal);

        // Новый пользователь — первый посетитель этого кода. Деление на ноль
        // или NullReference здесь означали бы 500 на пустом аккаунте.
        response.TotalTasks.Should().Be(0);
        response.CompletedTasks.Should().Be(0);
    }

    [Fact]
    public void Задача_без_цели_отдаёт_пустой_заголовок_цели()
    {
        var task = new TaskItem { UserId = Guid.NewGuid(), Title = "Купить хлеб", Goal = null };

        var response = _mapper.Map<TaskResponse>(task);

        response.GoalId.Should().BeNull();
        response.GoalTitle.Should().BeNull();
    }

    [Fact]
    public void Задача_с_целью_подставляет_её_название()
    {
        var goal = new Goal { Title = "Защитить диплом" };
        var task = new TaskItem { Title = "Написать тесты", GoalId = goal.Id, Goal = goal };

        var response = _mapper.Map<TaskResponse>(task);

        response.GoalTitle.Should().Be("Защитить диплом");
    }

    [Fact]
    public void Внутренний_путь_в_хранилище_наружу_не_уходит()
    {
        var file = new StoredFile
        {
            UserId = Guid.NewGuid(),
            FileName = "резюме.pdf",
            FirebaseUrl = "https://storage.example/резюме.pdf",
            StoragePath = "users/2f1c/career/резюме.pdf",
            ContentType = "application/pdf",
            SizeBytes = 12_345,
            Module = ModuleType.Career
        };

        var response = _mapper.Map<FileResponse>(file);

        response.Url.Should().Be(file.FirebaseUrl);

        // StoragePath — деталь устройства хранилища. Отдавать его клиенту
        // значило бы раскрывать структуру бакета и идентификаторы пользователей.
        typeof(FileResponse).GetProperty("StoragePath").Should().BeNull();
    }

    [Fact]
    public void Профиль_пользователя_не_содержит_хеша_пароля()
    {
        var user = new User
        {
            Name = "Дан",
            Surname = "Абубек",
            Email = "dan@lifeos.kz",
            PasswordHash = "$2a$12$секретныйХеш",
            Role = UserRole.Admin
        };

        var response = _mapper.Map<UserResponse>(user);

        response.Email.Should().Be("dan@lifeos.kz");
        response.Role.Should().Be(UserRole.Admin);
        typeof(UserResponse).GetProperty("PasswordHash").Should().BeNull();
    }
}
