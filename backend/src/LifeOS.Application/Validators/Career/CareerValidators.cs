using FluentValidation;
using LifeOS.Application.DTO.Career;

namespace LifeOS.Application.Validators.Career;

public class UpdateCareerProfileRequestValidator : AbstractValidator<UpdateCareerProfileRequest>
{
    public UpdateCareerProfileRequestValidator()
    {
        RuleFor(x => x.Skills)
            .MaximumLength(1000).WithMessage("Список навыков не длиннее 1000 символов.");

        RuleFor(x => x.DesiredPosition)
            .MaximumLength(200).WithMessage("Название позиции не длиннее 200 символов.");
    }
}
