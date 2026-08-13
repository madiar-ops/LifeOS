using FluentValidation;
using LifeOS.Application.DTO.Finance;

namespace LifeOS.Application.Validators.Finance;

public class UpdateTransactionRequestValidator : AbstractValidator<UpdateTransactionRequest>
{
    public UpdateTransactionRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum().WithMessage("Тип операции должен быть Income или Expense.");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Категория обязательна.")
            .MaximumLength(100).WithMessage("Категория не длиннее 100 символов.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Сумма должна быть больше нуля.")
            .LessThanOrEqualTo(999_999_999m).WithMessage("Сумма слишком велика.");

        RuleFor(x => x.Currency)
            .NotEmpty().WithMessage("Валюта обязательна.")
            .Matches("^[A-Za-z]{3}$").WithMessage("Валюта в формате ISO 4217, например KZT или USD.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Описание не длиннее 500 символов.");

        RuleFor(x => x.Date)
            .NotEqual(default(DateOnly)).WithMessage("Дата операции обязательна.");
    }
}
