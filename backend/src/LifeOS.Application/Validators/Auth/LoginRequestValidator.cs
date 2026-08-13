using FluentValidation;
using LifeOS.Application.DTO.Auth;

namespace LifeOS.Application.Validators.Auth;

/// <summary>
/// При входе НЕ проверяем сложность пароля — только его наличие.
/// Иначе форма логина подсказывала бы злоумышленнику правила формирования паролей.
/// </summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email обязателен.")
            .EmailAddress().WithMessage("Некорректный формат email.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Пароль обязателен.");
    }
}
