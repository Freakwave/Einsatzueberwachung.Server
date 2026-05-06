namespace Einsatzueberwachung.Domain.Models.Enums
{
    /// <summary>
    /// Unterscheidet zwischen einem GPS-Halsband-Track und einem manuell importierten Mensch-Laufweg.
    /// </summary>
    public enum TrackType
    {
        /// <summary>Track wurde vom GPS-Halsband aufgezeichnet (Live-Recording oder GPX-Import).</summary>
        CollarTrack,

        /// <summary>Track wurde als Mensch-Laufweg (z.B. von Smartphone-App) importiert.</summary>
        HumanTrack
    }
}
