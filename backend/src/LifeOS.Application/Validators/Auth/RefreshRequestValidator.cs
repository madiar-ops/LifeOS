using FluentValidation;
using LifeOS.Application.DTO.Auth;

namespace LifeOS.Application.Validators.Auth;

public class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithMessage("Refresh-токен обязателен.")
            .MaximumLength(200).WithMessage("Некорректный refresh-токен.");
    }
}
