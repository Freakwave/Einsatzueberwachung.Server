using FluentValidation;
using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Domain.Validators;

/// <summary>
/// Validator für AppSettings
/// </summary>
public class AppSettingsValidator : AbstractValidator<AppSettings>
{
    public AppSettingsValidator()
    {
        RuleFor(s => s.DefaultFirstWarningMinutes)
            .GreaterThan(0).WithMessage("1. Timer-Warnung muss größer als 0 sein")
            .LessThanOrEqualTo(90).WithMessage("1. Timer-Warnung sollte nicht mehr als 90 Minuten sein");

        RuleFor(s => s.DefaultSecondWarningMinutes)
            .GreaterThan(0).WithMessage("2. Timer-Warnung muss größer als 0 sein")
            .LessThanOrEqualTo(120).WithMessage("2. Timer-Warnung sollte nicht mehr als 120 Minuten sein")
            .GreaterThan(s => s.DefaultFirstWarningMinutes)
            .WithMessage("2. Timer-Warnung muss größer als 1. Timer-Warnung sein");

        RuleFor(s => s.ThemeMode)
            .NotEmpty().WithMessage("Theme-Modus ist erforderlich");
    }
}
