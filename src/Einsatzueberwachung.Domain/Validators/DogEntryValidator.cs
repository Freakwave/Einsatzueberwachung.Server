using FluentValidation;
using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Domain.Validators;

/// <summary>
/// Validator für DogEntry
/// </summary>
public class DogEntryValidator : AbstractValidator<DogEntry>
{
    public DogEntryValidator()
    {
        RuleFor(d => d.Name)
            .NotEmpty().WithMessage("Hundename ist erforderlich")
            .MinimumLength(2).WithMessage("Name muss mindestens 2 Zeichen haben")
            .MaximumLength(50).WithMessage("Name darf maximal 50 Zeichen haben");

        RuleFor(d => d.Rasse)
            .NotEmpty().WithMessage("Rasse ist erforderlich")
            .MaximumLength(100).WithMessage("Rasse darf maximal 100 Zeichen haben");
    }
}
