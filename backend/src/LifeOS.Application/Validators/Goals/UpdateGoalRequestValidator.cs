using FluentValidation;
using LifeOS.Application.DTO.Goals;

namespace LifeOS.Application.Validators.Goals;

public class UpdateGoalRequestValidator : AbstractValidator<UpdateGoalRequest>
{
    public UpdateGoalRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Название цели обязательно.")
            .MaximumLength(200).WithMessage("Название не длиннее 200 символов.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Описание не длиннее 2000 символов.");

        RuleFor(x => x.Status).IsInEnum().WithMessage("Недопустимый статус цели.");
        RuleFor(x => x.Priority).IsInEnum().WithMessage("Недопустимый приоритет.");
    }
}
