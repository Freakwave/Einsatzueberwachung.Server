using FluentValidation;
using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Domain.Validators;

/// <summary>
/// Validator für DroneEntry
/// </summary>
public class DroneEntryValidator : AbstractValidator<DroneEntry>
{
    public DroneEntryValidator()
    {
        RuleFor(d => d.Name)
            .NotEmpty().WithMessage("Drohnenname ist erforderlich")
            .MinimumLength(2).WithMessage("Name muss mindestens 2 Zeichen haben")
            .MaximumLength(100).WithMessage("Name darf maximal 100 Zeichen haben");

        RuleFor(d => d.Modell)
            .NotEmpty().WithMessage("Modell ist erforderlich")
            .MaximumLength(100).WithMessage("Modell darf maximal 100 Zeichen haben");

        RuleFor(d => d.Seriennummer)
            .NotEmpty().WithMessage("Seriennummer ist erforderlich")
            .MaximumLength(50).WithMessage("Seriennummer darf maximal 50 Zeichen haben")
            .Matches(@"^[A-Z0-9\-]+$").WithMessage("Seriennummer darf nur Großbuchstaben, Zahlen und Bindestriche enthalten")
            .When(d => !string.IsNullOrWhiteSpace(d.Seriennummer));

        RuleFor(d => d.LivestreamUrl)
            .MaximumLength(500).WithMessage("Livestream-URL darf maximal 500 Zeichen haben")
            .Must(url => string.IsNullOrWhiteSpace(url) || Uri.TryCreate(url, UriKind.Absolute, out _))
            .WithMessage("Livestream-URL muss eine gültige URL sein")
            .When(d => !string.IsNullOrWhiteSpace(d.LivestreamUrl));
    }
}
