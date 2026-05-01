using System;

namespace Einsatzueberwachung.Domain.Models
{
    public class DroneEntry
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Modell { get; set; }
        public string Hersteller { get; set; }
        public string Seriennummer { get; set; }
        public string DrohnenpilotId { get; set; }
        public string Notizen { get; set; }
        public string LivestreamUrl { get; set; }
        public bool IsActive { get; set; }

        public DroneEntry()
        {
            Id = Guid.NewGuid().ToString();
            Name = string.Empty;
            Modell = string.Empty;
            Hersteller = string.Empty;
            Seriennummer = string.Empty;
            DrohnenpilotId = string.Empty;
            Notizen = string.Empty;
            LivestreamUrl = string.Empty;
            IsActive = true;
        }

        public string DisplayName => !string.IsNullOrWhiteSpace(Name) ? Name : Modell;
        public string FullDescription => $"{Hersteller} {Modell}".Trim();
    }
}
