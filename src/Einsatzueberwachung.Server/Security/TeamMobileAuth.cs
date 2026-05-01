namespace Einsatzueberwachung.Server.Security;

public static class TeamMobileAuth
{
    public const string AuthenticationScheme = "TeamMobile";
    public const string AuthorizationPolicy = "TeamMobileOnly";
    public const string CookieName = "einsatz.team.auth";
    public const string TeamIdClaim = "team-id";
    public const string GenerationClaim = "team-generation";
}

public sealed class TeamMobileOptions
{
    public const string SectionName = "TeamMobile";

    /// <summary>
    /// Öffentliche Basis-URL der Mobile-Ansicht (z.B. https://dein-einsatz.mywire.org).
    /// Wenn leer, wird die URL aus dem Request abgeleitet.
    /// </summary>
    public string? PublicBaseUrl { get; set; }
}
