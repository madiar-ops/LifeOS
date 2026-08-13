using FluentValidation;
using LifeOS.Application.DTO.Finance;

namespace LifeOS.Application.Validators.Finance;

public class CreateTransactionRequestValidator : AbstractValidator<CreateTransactionRequest>
{
    public CreateTransactionRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum().WithMessage("Тип операции должен быть Income или Expense.");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Категория обязательна.")
            .MaximumLength(100).WithMessage("Категория не длиннее 100 символов.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Сумма должна быть больше нуля.")
            // Ограничение сверху защищает от опечатки в 10 нулей,
            // которая исказит всю статистику и графики.
            .LessThanOrEqualTo(999_999_999m).WithMessage("Сумма слишком велика.");

        // ISO 4217 — ровно три латинские буквы.
        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Валюта обязательна.")
            .Matches("^[A-Za-z]{3}$").WithMessage("Валюта в формате ISO 4217, например KZT или USD.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Описание не длиннее 500 символов.");

        RuleFor(x => x.Date)
            .NotEqual(default(DateOnly)).WithMessage("Дата операции обязательна.");
    }
}
