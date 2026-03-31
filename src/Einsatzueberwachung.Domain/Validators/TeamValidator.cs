using FluentValidation;
using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Domain.Validators;

/// <summary>
/// Validator für Team
/// </summary>
public class TeamValidator : AbstractValidator<Team>
{
    public TeamValidator()
    {
        RuleFor(t => t.TeamId)
            .NotEmpty().WithMessage("Team-ID ist erforderlich");

        // Hunde-Team muss Hund haben
        When(t => !t.IsDroneTeam && !t.IsSupportTeam, () =>
        {
            RuleFor(t => t.DogId)
                .NotEmpty().WithMessage("Hund ist für Hunde-Teams erforderlich");

            RuleFor(t => t.FirstWarningMinutes)
                .GreaterThan(0).WithMessage("1. Warnung muss größer als 0 sein")
                .LessThanOrEqualTo(90).WithMessage("1. Warnung sollte nicht mehr als 90 Minuten sein");

            RuleFor(t => t.SecondWarningMinutes)
                .GreaterThan(0).WithMessage("2. Warnung muss größer als 0 sein")
                .LessThanOrEqualTo(120).WithMessage("2. Warnung sollte nicht mehr als 120 Minuten sein")
                .GreaterThan(t => t.FirstWarningMinutes)
                .WithMessage("2. Warnung muss größer als 1. Warnung sein");
        });

        // Drohnen-Team muss Drohne haben
        When(t => t.IsDroneTeam, () =>
        {
            RuleFor(t => t.DroneId)
                .NotEmpty().WithMessage("Drohne ist für Drohnen-Teams erforderlich");
        });
    }
}
