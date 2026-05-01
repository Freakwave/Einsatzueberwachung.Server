using System.Text.Json;
using Einsatzueberwachung.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Einsatzueberwachung.Domain.Services
{
    public partial class DwdWeatherService
    {
        public async Task<FlugwetterData?> GetFlugwetterAsync(double latitude, double longitude)
        {
            try
            {
                _logger?.LogInformation("Fetching Flugwetter data for lat={Lat}, lon={Lon}", latitude, longitude);

                var url = $"{BrightSkyBaseUrl}/current_weather?lat={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lon={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("Flugwetter API returned status {StatusCode}", response.StatusCode);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var weatherResponse = JsonSerializer.Deserialize<BrightSkyCurrentWeatherResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (weatherResponse?.Weather == null)
                {
                    _logger?.LogWarning("No weather data in Flugwetter response");
                    return null;
                }

                var data = weatherResponse.Weather;
                var source = weatherResponse.Sources?.FirstOrDefault();

                var temperatur = data.Temperature ?? 0;
                var taupunkt = data.DewPoint ?? 0;
                var spread = temperatur - taupunkt;

                var wolkenuntergrenze = spread > 0 ? spread * 125 : 0;

                var sichtweiteM = data.Visibility ?? 10000;
                var windSpeed = data.GetWindSpeed();
                var windGust = data.GetWindGustSpeed();
                var windDir = data.GetWindDirection();

                var kategorie = BestimmeFlugkategorie(sichtweiteM, wolkenuntergrenze);

                var (istMoeglich, hinweis) = BewerteDrohnenflug(windSpeed, windGust, sichtweiteM, kategorie, data.Condition);

                var flugwetter = new FlugwetterData
                {
                    Zeitpunkt = data.Timestamp ?? Now,
                    StationsName = source?.StationName ?? "Unbekannte Station",
                    StationsId = source?.Id?.ToString() ?? "",
                    Entfernung = 0,

                    Windgeschwindigkeit = windSpeed,
                    Windboeen = windGust,
                    Windrichtung = (int)windDir,
                    WindrichtungText = GetWindrichtungText((int)windDir),

                    Sichtweite = sichtweiteM / 1000.0,
                    SichtweiteRoh = sichtweiteM,

                    Wolkenuntergrenze = (int)wolkenuntergrenze,
                    WolkenuntergrenzeFuss = (int)(wolkenuntergrenze * 3.281),
                    Wolkenbedeckung = (int)(data.CloudCover ?? 0),

                    Temperatur = temperatur,
                    Taupunkt = taupunkt,
                    Spread = spread,

                    Niederschlag = data.GetPrecipitation(),
                    QNH = (int)(data.PressureMsl ?? 1013),

                    Kategorie = kategorie,
                    IstDrohnenflugMoeglich = istMoeglich,
                    DrohnenflugHinweis = hinweis,

                    MetarRaw = GeneratePseudoMetar(data, source?.StationName)
                };

                _logger?.LogInformation("Flugwetter parsed: Kategorie={Kat}, Sicht={Sicht}m, Ceiling={Ceil}ft, Wind={Wind}km/h",
                    kategorie, sichtweiteM, flugwetter.WolkenuntergrenzeFuss, windSpeed);

                return flugwetter;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Exception while fetching Flugwetter data");
                return null;
            }
        }

        private FlugKategorie BestimmeFlugkategorie(double sichtweiteM, double wolkenuntergrenze)
        {
            var ceilingFt = wolkenuntergrenze * 3.281;
            var visibilitySM = sichtweiteM / 1609.34;

            FlugKategorie sichtKat, ceilingKat;

            if (visibilitySM < 1)
                sichtKat = FlugKategorie.LIFR;
            else if (visibilitySM < 3)
                sichtKat = FlugKategorie.IFR;
            else if (visibilitySM < 5)
                sichtKat = FlugKategorie.MVFR;
            else
                sichtKat = FlugKategorie.VFR;

            if (ceilingFt < 500)
                ceilingKat = FlugKategorie.LIFR;
            else if (ceilingFt < 1000)
                ceilingKat = FlugKategorie.IFR;
            else if (ceilingFt < 3000)
                ceilingKat = FlugKategorie.MVFR;
            else
                ceilingKat = FlugKategorie.VFR;

            return (FlugKategorie)Math.Max((int)sichtKat, (int)ceilingKat);
        }

        private (bool istMoeglich, string hinweis) BewerteDrohnenflug(
            double windSpeed, double windGust, double sichtweiteM,
            FlugKategorie kategorie, string? condition)
        {
            var hinweise = new List<string>();
            var istMoeglich = true;

            if (windGust > 38)
            {
                istMoeglich = false;
                hinweise.Add($"Windböen zu stark ({windGust:F0} km/h)");
            }
            else if (windGust > 28)
            {
                hinweise.Add($"Starke Böen beachten ({windGust:F0} km/h)");
            }

            if (windSpeed > 30)
            {
                istMoeglich = false;
                hinweise.Add($"Windgeschwindigkeit zu hoch ({windSpeed:F0} km/h)");
            }
            else if (windSpeed > 20)
            {
                hinweise.Add($"Erhöhte Windgeschwindigkeit ({windSpeed:F0} km/h)");
            }

            if (sichtweiteM < 1000)
            {
                istMoeglich = false;
                hinweise.Add("Sichtweite unter 1 km - Sichtflug nicht möglich");
            }
            else if (sichtweiteM < 3000)
            {
                hinweise.Add("Eingeschränkte Sicht");
            }

            var schlechtesWetter = new[] { "rain", "snow", "sleet", "hail", "thunderstorm", "fog" };
            if (!string.IsNullOrWhiteSpace(condition) && schlechtesWetter.Contains(condition.ToLowerInvariant()))
            {
                var wetterHinweis = condition.ToLowerInvariant() switch
                {
                    "thunderstorm" => "Gewitter - Flugbetrieb einstellen!",
                    "rain" => "Regen - Drohne kann beschädigt werden",
                    "snow" => "Schnee - Eingeschränkte Sicht und Akku-Performance",
                    "fog" => "Nebel - Sichtflug nicht möglich",
                    "hail" => "Hagel - Flugbetrieb einstellen!",
                    _ => "Niederschlag - Vorsicht geboten"
                };

                if (condition.ToLowerInvariant() == "thunderstorm" || condition.ToLowerInvariant() == "hail")
                    istMoeglich = false;

                hinweise.Add(wetterHinweis);
            }

            if (kategorie == FlugKategorie.LIFR)
            {
                istMoeglich = false;
                hinweise.Add("LIFR-Bedingungen - Kein Sichtflug möglich");
            }
            else if (kategorie == FlugKategorie.IFR)
            {
                hinweise.Add("IFR-Bedingungen - Nur mit besonderer Vorsicht");
            }

            if (!hinweise.Any())
            {
                hinweise.Add("Gute Flugbedingungen");
            }

            return (istMoeglich, string.Join(" | ", hinweise));
        }

        private string GeneratePseudoMetar(BrightSkyWeatherData data, string? stationName)
        {
            var zeitUtc = (data.Timestamp ?? DateTime.UtcNow).ToString("ddHHmm") + "Z";
            var wind = $"{(int)(data.GetWindDirection()):D3}{(int)(data.GetWindSpeed() / 1.852):D2}";
            if (data.GetWindGustSpeed() > 0)
                wind += $"G{(int)(data.GetWindGustSpeed() / 1.852):D2}";
            wind += "KT";

            var vis = data.Visibility.HasValue ? $"{Math.Min(9999, (int)data.Visibility.Value):D4}" : "9999";
            var temp = $"{(data.Temperature >= 0 ? "" : "M")}{Math.Abs((int)(data.Temperature ?? 0)):D2}/{(data.DewPoint >= 0 ? "" : "M")}{Math.Abs((int)(data.DewPoint ?? 0)):D2}";
            var qnh = $"Q{(int)(data.PressureMsl ?? 1013)}";

            var station = "ZZZZ";
            if (!string.IsNullOrWhiteSpace(stationName))
            {
                var stationCode = stationName.ToUpperInvariant().Replace(" ", string.Empty);
                if (stationCode.Length >= 4)
                {
                    station = stationCode.Substring(0, 4);
                }
                else if (stationCode.Length > 0)
                {
                    station = stationCode.PadRight(4, 'X');
                }
            }

            return $"{station} {zeitUtc} {wind} {vis} {temp} {qnh} (Berechnet)";
        }
    }
}
