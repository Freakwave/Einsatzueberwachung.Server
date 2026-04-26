// Service-Interface fuer Wetter-Abfragen vom Deutschen Wetterdienst (DWD)

using System;
using System.Threading.Tasks;
using Einsatzueberwachung.Domain.Models;

namespace Einsatzueberwachung.Domain.Interfaces
{
    public interface IWeatherService
    {
        /// <summary>
        /// Holt aktuelle Wetterdaten fuer eine Position
        /// </summary>
        Task<WeatherData?> GetCurrentWeatherAsync(double latitude, double longitude);

        /// <summary>
        /// Holt aktuelle Wetterdaten fuer eine Adresse/Ort
        /// </summary>
        Task<WeatherData?> GetCurrentWeatherByAddressAsync(string address);

        /// <summary>
        /// Ermittelt Koordinaten zu einer Adresse/Ort
        /// </summary>
        Task<(double Latitude, double Longitude)?> GeocodeAddressAsync(string address);

        /// <summary>
        /// Holt Wettervorhersage fuer die naechsten Stunden
        /// </summary>
        Task<WeatherForecast?> GetForecastAsync(double latitude, double longitude);

        /// <summary>
        /// Holt Flugwetter-Daten (METAR/Synop) fuer eine Position
        /// </summary>
        Task<FlugwetterData?> GetFlugwetterAsync(double latitude, double longitude);
    }
}
