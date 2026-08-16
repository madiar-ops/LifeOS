using FluentValidation;
using LifeOS.Application.DTO.Study;

namespace LifeOS.Application.Validators.Study;

public class CreateStudyMaterialRequestValidator : AbstractValidator<CreateStudyMaterialRequest>
{
    public CreateStudyMaterialRequestValidator()
    {
        RuleFor(x => x.FileId).NotEmpty().WithMessage("Файл обязателен.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Название материала обязательно.")
            .MaximumLength(200).WithMessage("Название не длиннее 200 символов.");
    }
}

public class GenerateQuizRequestValidator : AbstractValidator<GenerateQuizRequest>
{
    public GenerateQuizRequestValidator()
    {
        RuleFor(x => x.StudyMaterialId).NotEmpty().WithMessage("Материал обязателен.");

        // Верхняя граница совпадает с ограничением AI-сервиса:
        // расхождение давало бы 422 от FastAPI вместо понятной 400.
        RuleFor(x => x.QuestionCount)
            .InclusiveBetween(1, 15).WithMessage("Количество вопросов — от 1 до 15.");
    }
}

public class SubmitQuizRequestValidator : AbstractValidator<SubmitQuizRequest>
{
    public SubmitQuizRequestValidator()
    {
        RuleFor(x => x.Answers)
            .NotEmpty().WithMessage("Ответы обязательны.");

        RuleForEach(x => x.Answers)
            .GreaterThanOrEqualTo(0).WithMessage("Индекс ответа не может быть отрицательным.");
    }
}

public class CreateStudyNoteRequestValidator : AbstractValidator<CreateStudyNoteRequest>
{
    public CreateStudyNoteRequestValidator()
    {
        RuleFor(x => x.StudyMaterialId).NotEmpty().WithMessage("Материал обязателен.");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Текст заметки обязателен.")
            .MaximumLength(10_000).WithMessage("Заметка не длиннее 10000 символов.");
    }
}

public class UpdateStudyNoteRequestValidator : AbstractValidator<UpdateStudyNoteRequest>
{
    public UpdateStudyNoteRequestValidator()
    {
        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Текст заметки обязателен.")
            .MaximumLength(10_000).WithMessage("Заметка не длиннее 10000 символов.");
    }
}
