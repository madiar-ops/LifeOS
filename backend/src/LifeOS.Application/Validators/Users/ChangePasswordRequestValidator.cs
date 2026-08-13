using FluentValidation;
using LifeOS.Application.DTO.Users;

namespace LifeOS.Application.Validators.Users;

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Текущий пароль обязателен.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Новый пароль обязателен.")
            .MinimumLength(8).WithMessage("Пароль не короче 8 символов.")
            .MaximumLength(128).WithMessage("Пароль не длиннее 128 символов.")
            .Matches("[A-Z]").WithMessage("Пароль должен содержать хотя бы одну заглавную букву.")
            .Matches("[a-z]").WithMessage("Пароль должен содержать хотя бы одну строчную букву.")
            .Matches("[0-9]").WithMessage("Пароль должен содержать хотя бы одну цифру.")
            .NotEqual(x => x.CurrentPassword).WithMessage("Новый пароль должен отличаться от текущего.");
    }
}
