namespace Einsatzueberwachung.Domain.Interfaces
{
    /// <summary>
    /// Liefert die aktuelle Zeit in der konfigurierten Zeitzone des Servers.
    /// Alle Zeitstempel für Notizen, Funksprüche und Einsatz-Events werden darüber erzeugt.
    /// </summary>
    public interface ITimeService
    {
        /// <summary>Die aktuelle Zeit in der konfigurierten Zeitzone (z.B. Europe/Berlin).</summary>
        DateTime Now { get; }

        /// <summary>
        /// Zeitzone neu laden – aufrufen nachdem AppSettings.TimeZoneId geändert wurde.
        /// </summary>
        void Refresh();
    }
}
