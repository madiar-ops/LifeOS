using FluentValidation;
using LifeOS.Application.DTO.Tasks;

namespace LifeOS.Application.Validators.Tasks;

public class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Название задачи обязательно.")
            .MaximumLength(200).WithMessage("Название не длиннее 200 символов.");

        RuleFor(x => x.GoalId)
            .NotEqual(Guid.Empty).WithMessage("Некорректный идентификатор цели.")
            .When(x => x.GoalId.HasValue);
    }
}
