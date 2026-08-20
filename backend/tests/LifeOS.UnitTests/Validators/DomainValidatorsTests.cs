using FluentAssertions;
using FluentValidation.TestHelper;
using LifeOS.Application.DTO.Finance;
using LifeOS.Application.DTO.Goals;
using LifeOS.Application.DTO.Health;
using LifeOS.Application.DTO.Study;
using LifeOS.Application.DTO.Tasks;
using LifeOS.Application.Validators.Finance;
using LifeOS.Application.Validators.Goals;
using LifeOS.Application.Validators.Health;
using LifeOS.Application.Validators.Study;
using LifeOS.Application.Validators.Tasks;
using LifeOS.Domain.Enums;

namespace LifeOS.UnitTests.Validators;

public class CreateGoalRequestValidatorTests
{
    private readonly CreateGoalRequestValidator _sut = new();

    [Fact]
    public void Пустое_название_отклоняется()
        => _sut.TestValidate(Request(title: "   ")).ShouldHaveValidationErrorFor(x => x.Title);

    [Fact]
    public void Название_длиннее_200_символов_отклоняется()
        => _sut.TestValidate(Request(title: new string('ц', 201)))
               .ShouldHaveValidationErrorFor(x => x.Title);

    [Fact]
    public void Несуществующее_значение_статуса_отклоняется()
    {
        // Без IsInEnum значение 99 дошло бы до БД: enum в C# — это просто int,
        // и «недопустимых» значений для него на уровне типа не существует.
        var result = _sut.TestValidate(Request(status: (GoalStatus)99));

        result.ShouldHaveValidationErrorFor(x => x.Status);
    }

    [Fact]
    public void Несуществующее_значение_приоритета_отклоняется()
        => _sut.TestValidate(Request(priority: (PriorityLevel)42))
               .ShouldHaveValidationErrorFor(x => x.Priority);

    [Fact]
    public void Цель_без_описания_и_дедлайна_допустима()
    {
        // Описание и срок необязательны: цель «выучить английский» не обязана
        // иметь дату окончания, и заставлять придумывать её — плохой UX.
        var result = _sut.TestValidate(new CreateGoalRequest(
            "Выучить английский", null, GoalStatus.NotStarted, PriorityLevel.Medium, null));

        result.IsValid.Should().BeTrue();
    }

    private static CreateGoalRequest Request(
        string title = "Защитить диплом",
        GoalStatus status = GoalStatus.InProgress,
        PriorityLevel priority = PriorityLevel.High)
        => new(title, "описание", status, priority, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
}

public class CreateTaskRequestValidatorTests
{
    private readonly CreateTaskRequestValidator _sut = new();

    [Fact]
    public void Пустое_название_отклоняется()
        => _sut.TestValidate(new CreateTaskRequest("", null, null))
               .ShouldHaveValidationErrorFor(x => x.Title);

    [Fact]
    public void Задача_без_цели_допустима()
    {
        // Самостоятельная задача — штатный сценарий: не всё в жизни
        // обязано принадлежать какой-то цели.
        var result = _sut.TestValidate(new CreateTaskRequest("Купить хлеб", null, null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Пустой_GUID_цели_отклоняется()
    {
        // Guid.Empty — это не «цели нет», а «прислали мусор».
        // Для «цели нет» существует null.
        var result = _sut.TestValidate(new CreateTaskRequest("Задача", Guid.Empty, null));

        result.ShouldHaveValidationErrorFor(x => x.GoalId);
    }
}

public class CreateTransactionRequestValidatorTests
{
    private readonly CreateTransactionRequestValidator _sut = new();

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Неположительная_сумма_отклоняется(int amount)
    {
        // Знак операции задаётся полем Type, а не знаком суммы. Отрицательный
        // расход означал бы доход — и вся статистика посчиталась бы наоборот.
        var result = _sut.TestValidate(Request(amount: amount));

        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void Сумма_с_лишними_нулями_отклоняется()
    {
        // Верхняя граница ловит опечатку в десять нулей: одна такая запись
        // растянет ось всех графиков и обесценит остальные данные.
        var result = _sut.TestValidate(Request(amount: 1_000_000_000m));

        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Theory]
    [InlineData("KZ")]
    [InlineData("KZTT")]
    [InlineData("₸")]
    [InlineData("")]
    public void Валюта_не_по_ISO_4217_отклоняется(string currency)
        => _sut.TestValidate(Request(currency: currency)).ShouldHaveValidationErrorFor(x => x.Currency);

    [Theory]
    [InlineData("KZT")]
    [InlineData("usd")]
    [InlineData("Eur")]
    public void Три_латинские_буквы_в_любом_регистре_принимаются(string currency)
        => _sut.TestValidate(Request(currency: currency)).ShouldNotHaveValidationErrorFor(x => x.Currency);

    [Fact]
    public void Дата_по_умолчанию_отклоняется()
    {
        // DateOnly по умолчанию — 01.01.0001. Это признак того, что поле
        // просто не пришло, а не что операция совершена две тысячи лет назад.
        var result = _sut.TestValidate(Request(date: default));

        result.ShouldHaveValidationErrorFor(x => x.Date);
    }

    [Fact]
    public void Несуществующий_тип_операции_отклоняется()
        => _sut.TestValidate(Request(type: (TransactionType)7))
               .ShouldHaveValidationErrorFor(x => x.Type);

    [Fact]
    public void Обычная_операция_проходит()
        => _sut.TestValidate(Request()).IsValid.Should().BeTrue();

    private static CreateTransactionRequest Request(
        TransactionType type = TransactionType.Expense,
        string category = "Продукты",
        decimal amount = 12_500m,
        string currency = "KZT",
        DateOnly? date = null)
        => new(type, category, amount, currency,
               date ?? new DateOnly(2026, 3, 1), "обед");
}

public class CreateHealthLogRequestValidatorTests
{
    private readonly CreateHealthLogRequestValidator _sut = new();

    [Theory]
    [InlineData(19)]
    [InlineData(401)]
    public void Физиологически_невозможный_вес_отклоняется(int weight)
    {
        // Границы отсекают опечатку (700 кг вместо 70), а не судят о здоровье:
        // валидатор не вправе решать, какой вес «нормальный».
        var result = _sut.TestValidate(Request(weight: weight));

        result.ShouldHaveValidationErrorFor(x => x.Weight);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(400)]
    [InlineData(72.5)]
    public void Вес_в_допустимых_границах_принимается(double weight)
        => _sut.TestValidate(Request(weight: (decimal)weight))
               .ShouldNotHaveValidationErrorFor(x => x.Weight);

    [Fact]
    public void Незаполненный_вес_допустим()
    {
        // «Не взвешивался» и «весил 0 кг» — разные утверждения. Пустое поле
        // обязано оставаться пустым, иначе ноль попадёт в датасет модели.
        var result = _sut.TestValidate(Request(weight: null));

        result.ShouldNotHaveValidationErrorFor(x => x.Weight);
    }

    [Fact]
    public void Сон_дольше_суток_отклоняется()
        => _sut.TestValidate(Request(sleep: 25m)).ShouldHaveValidationErrorFor(x => x.SleepHours);

    [Fact]
    public void Настроение_вне_шкалы_отклоняется()
    {
        // MoodLevel начинается с 1, поэтому ноль — это «поле не пришло»,
        // а не «очень плохо».
        var result = _sut.TestValidate(Request(mood: (MoodLevel)0));

        result.ShouldHaveValidationErrorFor(x => x.Mood);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(20_001)]
    public void Невозможный_объём_воды_отклоняется(int waterMl)
        => _sut.TestValidate(Request(waterMl: waterMl)).ShouldHaveValidationErrorFor(x => x.WaterMl);

    [Fact]
    public void Невозможное_количество_шагов_отклоняется()
        => _sut.TestValidate(Request(steps: 200_001)).ShouldHaveValidationErrorFor(x => x.Steps);

    [Fact]
    public void День_без_единого_измерения_допустим()
    {
        // Запись «сегодня ничего не измерял» имеет смысл: она фиксирует сам факт
        // ведения дневника и отличается от отсутствия записи за день.
        var result = _sut.TestValidate(new CreateHealthLogRequest(
            new DateOnly(2026, 3, 1), null, null, MoodLevel.Neutral, 0, 0));

        result.IsValid.Should().BeTrue();
    }

    private static CreateHealthLogRequest Request(
        decimal? weight = 72m,
        decimal? sleep = 7.5m,
        MoodLevel mood = MoodLevel.Good,
        int waterMl = 2000,
        int steps = 8000)
        => new(new DateOnly(2026, 3, 1), weight, sleep, mood, waterMl, steps);
}

public class StudyValidatorsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    public void Недопустимое_число_вопросов_теста_отклоняется(int count)
    {
        // Верхняя граница совпадает с ограничением AI-сервиса. Расхождение
        // давало бы 422 от FastAPI вместо понятной 400 от собственного API.
        var sut = new GenerateQuizRequestValidator();

        var result = sut.TestValidate(new GenerateQuizRequest(Guid.NewGuid(), count));

        result.ShouldHaveValidationErrorFor(x => x.QuestionCount);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    public void Число_вопросов_на_границах_принимается(int count)
        => new GenerateQuizRequestValidator()
               .TestValidate(new GenerateQuizRequest(Guid.NewGuid(), count))
               .ShouldNotHaveValidationErrorFor(x => x.QuestionCount);

    [Fact]
    public void Пустой_список_ответов_отклоняется()
        => new SubmitQuizRequestValidator()
               .TestValidate(new SubmitQuizRequest(new List<int>()))
               .ShouldHaveValidationErrorFor(x => x.Answers);

    [Fact]
    public void Отрицательный_индекс_ответа_отклоняется()
    {
        var result = new SubmitQuizRequestValidator()
            .TestValidate(new SubmitQuizRequest(new List<int> { 0, -1, 2 }));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Заметка_длиннее_10000_символов_отклоняется()
        => new CreateStudyNoteRequestValidator()
               .TestValidate(new CreateStudyNoteRequest(Guid.NewGuid(), new string('з', 10_001)))
               .ShouldHaveValidationErrorFor(x => x.Content);

    [Fact]
    public void Материал_без_файла_отклоняется()
        => new CreateStudyMaterialRequestValidator()
               .TestValidate(new CreateStudyMaterialRequest(Guid.Empty, "Лекция 1"))
               .ShouldHaveValidationErrorFor(x => x.FileId);
}
