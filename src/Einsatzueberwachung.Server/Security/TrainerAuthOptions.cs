namespace Einsatzueberwachung.Server.Security;

public sealed class TrainerAuthOptions
{
    public const string SectionName = "TrainerAuth";

    // Default according to current rollout requirement.
    public string Password { get; set; } = "trainer2026";
    public int SessionHours { get; set; } = 12;
}
