using FluentValidation;
using LifeOS.Application.DTO.Health;

namespace LifeOS.Application.Validators.Health;

/// <summary>
/// Границы значений подобраны как физиологически возможные, а не «нормальные»:
/// задача валидатора — отсечь опечатки (вес 700 кг, сон 30 часов),
/// а не судить о здоровье пользователя.
/// </summary>
public class CreateHealthLogRequestValidator : AbstractValidator<CreateHealthLogRequest>
{
    public CreateHealthLogRequestValidator()
    {
        RuleFor(x => x.Date)
            .NotEqual(default(DateOnly)).WithMessage("Дата записи обязательна.");

        RuleFor(x => x.Weight)
            .InclusiveBetween(20m, 400m).WithMessage("Вес должен быть в диапазоне 20–400 кг.")
            .When(x => x.Weight.HasValue);

        RuleFor(x => x.SleepHours)
            .InclusiveBetween(0m, 24m).WithMessage("Сон не может превышать 24 часа в сутки.")
            .When(x => x.SleepHours.HasValue);

        RuleFor(x => x.Mood).IsInEnum().WithMessage("Недопустимое значение настроения.");

        RuleFor(x => x.WaterMl)
            .InclusiveBetween(0, 20_000).WithMessage("Объём воды должен быть в диапазоне 0–20000 мл.");

        RuleFor(x => x.Steps)
            .InclusiveBetween(0, 200_000).WithMessage("Количество шагов должно быть в диапазоне 0–200000.");
    }
}
