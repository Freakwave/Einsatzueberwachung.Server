using System;

namespace Einsatzueberwachung.Domain.Models
{
    public class TeamPhoneLocation
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public DateTime Timestamp { get; set; }
        public double? Accuracy { get; set; }
    }
}
