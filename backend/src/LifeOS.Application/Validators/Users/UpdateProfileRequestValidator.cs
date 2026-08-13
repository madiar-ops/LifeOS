using FluentValidation;
using LifeOS.Application.DTO.Users;

namespace LifeOS.Application.Validators.Users;

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Имя обязательно.")
            .MaximumLength(100).WithMessage("Имя не длиннее 100 символов.");

        RuleFor(x => x.Surname)
            .NotEmpty().WithMessage("Фамилия обязательна.")
            .MaximumLength(100).WithMessage("Фамилия не длиннее 100 символов.");
    }
}
