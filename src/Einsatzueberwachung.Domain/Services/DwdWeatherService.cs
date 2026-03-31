// DWD Weather Service - Wetterdaten vom Deutschen Wetterdienst
// Verwendet die BrightSky API (Open Source Proxy fuer DWD Open Data)
// https://brightsky.dev/

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Einsatzueberwachung.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Einsatzueberwachung.Domain.Services
{
    public class DwdWeatherService : IWeatherService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DwdWeatherService>? _logger;
        private const string BrightSkyBaseUrl = "https://api.brightsky.dev";
        private const string NominatimBaseUrl = "https://nominatim.openstreetmap.org/search";
        
        // Cache fuer Wetterdaten (5 Minuten)
        private WeatherData? _cachedWeather;
        private DateTime _cacheTime = DateTime.MinValue;
        private (double lat, double lon) _cachedPosition;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public DwdWeatherService(HttpClient httpClient, ILogger<DwdWeatherService>? logger = null)
        {
            _httpClient = httpClient;
            _logger = logger;
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task<WeatherData?> GetCurrentWeatherAsync(double latitude, double longitude)
        {
            // Cache pruefen
            if (_cachedWeather != null && 
                DateTime.Now - _cacheTime < CacheDuration &&
                Math.Abs(_cachedPosition.lat - latitude) < 0.01 &&
                Math.Abs(_cachedPosition.lon - longitude) < 0.01)
            {
                return _cachedWeather;
            }

            try
            {
                // BrightSky API fuer aktuelles Wetter
                var url = $"{BrightSkyBaseUrl}/current_weather?lat={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lon={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                
                _logger?.LogInformation("Fetching weather from: {Url}", url);
                
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("API Error: {StatusCode}", response.StatusCode);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                _logger?.LogDebug("API Response: {Json}", json.Substring(0, Math.Min(300, json.Length)));
                
                var weatherResponse = JsonSerializer.Deserialize<BrightSkyCurrentWeatherResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (weatherResponse?.Weather == null)
                {
                    _logger?.LogWarning("Weather data is null after deserialization. JSON: {Json}", json.Substring(0, Math.Min(200, json.Length)));
                    return null;
                }

                _logger?.LogInformation("Parsed weather: Temp={Temp}C, Wind={Wind}km/h, Condition={Condition}", 
                    weatherResponse.Weather.Temperature, 
                    weatherResponse.Weather.GetWindSpeed(),
                    weatherResponse.Weather.Condition);
                
                var weather = MapToWeatherData(weatherResponse.Weather);
                
                // Cache aktualisieren
                _cachedWeather = weather;
                _cacheTime = DateTime.Now;
                _cachedPosition = (latitude, longitude);

                return weather;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Exception while fetching weather data");
                return null;
            }
        }

        public async Task<WeatherData?> GetCurrentWeatherByAddressAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                _logger?.LogWarning("Address-based weather lookup was called with an empty address.");
                return null;
            }

            try
            {
                var encodedAddress = Uri.EscapeDataString(address.Trim());
                var url = $"{NominatimBaseUrl}?q={encodedAddress}&format=jsonv2&limit=1&addressdetails=0";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("Einsatzueberwachung.Web/1.0 (+https://github.com/Elemirus1996/Einsatzueberwachung.Web)");
                request.Headers.Accept.ParseAdd("application/json");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger?.LogWarning("Nominatim geocoding failed with status {StatusCode} for address '{Address}'", response.StatusCode, address);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                {
                    _logger?.LogWarning("No geocoding result found for address '{Address}'", address);
                    return null;
                }

                var firstResult = doc.RootElement[0];
                if (!firstResult.TryGetProperty("lat", out var latElement) || !firstResult.TryGetProperty("lon", out var lonElement))
                {
                    _logger?.LogWarning("Geocoding response for '{Address}' does not contain coordinates", address);
                    return null;
                }

                var latText = latElement.GetString();
                var lonText = lonElement.GetString();

                if (!double.TryParse(latText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var latitude) ||
                    !double.TryParse(lonText, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var longitude))
                {
                    _logger?.LogWarning("Could not parse geocoding coordinates for '{Address}'. lat='{Lat}', lon='{Lon}'", address, latText, lonText);
                    return null;
                }

                _logger?.LogInformation("Address '{Address}' geocoded to lat={Lat}, lon={Lon}", address, latitude, longitude);
                return await GetCurrentWeatherAsync(latitude, longitude);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Exception while geocoding address '{Address}'", address);
                return null;
            }
        }

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

                // Temperaturen
                var temperatur = data.Temperature ?? 0;
                var taupunkt = data.DewPoint ?? 0;
                var spread = temperatur - taupunkt;
                
                // Wolkenuntergrenze nach Spread-Formel berechnen (Faustregel)
                // Spread in °C x 125m = Wolkenuntergrenze in Metern
                var wolkenuntergrenze = spread > 0 ? spread * 125 : 0;
                
                // Sichtweite (BrightSky liefert in Metern)
                var sichtweiteM = data.Visibility ?? 10000;
                // Wind
                var windSpeed = data.GetWindSpeed();
                var windGust = data.GetWindGustSpeed();
                var windDir = data.GetWindDirection();
                
                // Flugkategorie bestimmen
                var kategorie = BestimmeFlugkategorie(sichtweiteM, wolkenuntergrenze);
                
                // Bewertung für Drohnenflug
                var (istMoeglich, hinweis) = BewerteDrohnenflug(windSpeed, windGust, sichtweiteM, kategorie, data.Condition);

                var flugwetter = new FlugwetterData
                {
                    Zeitpunkt = data.Timestamp ?? DateTime.Now,
                    StationsName = source?.StationName ?? "Unbekannte Station",
                    StationsId = source?.Id?.ToString() ?? "",
                    Entfernung = 0, // BrightSky liefert keine Entfernung
                    
                    Windgeschwindigkeit = windSpeed,
                    Windboeen = windGust,
                    Windrichtung = (int)windDir,
                    WindrichtungText = GetWindrichtungText((int)windDir),
                    
                    Sichtweite = sichtweiteM / 1000.0, // in km
                    SichtweiteRoh = sichtweiteM, // in Metern für Kategorieberechnung
                    
                    Wolkenuntergrenze = (int)wolkenuntergrenze,
                    WolkenuntergrenzeFuss = (int)(wolkenuntergrenze * 3.281), // Meter zu Fuss
                    Wolkenbedeckung = (int)(data.CloudCover ?? 0),
                    
                    Temperatur = temperatur,
                    Taupunkt = taupunkt,
                    Spread = spread,
                    
                    Niederschlag = data.GetPrecipitation(),
                    QNH = (int)(data.PressureMsl ?? 1013),
                    
                    Kategorie = kategorie,
                    IstDrohnenflugMoeglich = istMoeglich,
                    DrohnenflugHinweis = hinweis,
                    
                    // Kein echtes METAR verfügbar über BrightSky
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
            // FAA-Kategorien:
            // LIFR: Visibility < 1 SM OR Ceiling < 500 ft
            // IFR: Visibility 1-3 SM OR Ceiling 500-1000 ft
            // MVFR: Visibility 3-5 SM OR Ceiling 1000-3000 ft
            // VFR: Visibility > 5 SM AND Ceiling > 3000 ft
            
            var ceilingFt = wolkenuntergrenze * 3.281; // Meter zu Fuss
            var visibilitySM = sichtweiteM / 1609.34; // Meter zu Statute Miles

            // Immer die restriktivere Kategorie wählen
            FlugKategorie sichtKat, ceilingKat;

            // Sichtkategorie
            if (visibilitySM < 1)
                sichtKat = FlugKategorie.LIFR;
            else if (visibilitySM < 3)
                sichtKat = FlugKategorie.IFR;
            else if (visibilitySM < 5)
                sichtKat = FlugKategorie.MVFR;
            else
                sichtKat = FlugKategorie.VFR;

            // Ceiling-Kategorie
            if (ceilingFt < 500)
                ceilingKat = FlugKategorie.LIFR;
            else if (ceilingFt < 1000)
                ceilingKat = FlugKategorie.IFR;
            else if (ceilingFt < 3000)
                ceilingKat = FlugKategorie.MVFR;
            else
                ceilingKat = FlugKategorie.VFR;

            // Restriktivste Kategorie zurückgeben
            return (FlugKategorie)Math.Max((int)sichtKat, (int)ceilingKat);
        }

        private (bool istMoeglich, string hinweis) BewerteDrohnenflug(
            double windSpeed, double windGust, double sichtweiteM, 
            FlugKategorie kategorie, string? condition)
        {
            var hinweise = new List<string>();
            var istMoeglich = true;

            // Windgrenzen für Drohnen (typisch DJI)
            if (windGust > 38) // > 38 km/h Böen
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

            // Sichtweite
            if (sichtweiteM < 1000)
            {
                istMoeglich = false;
                hinweise.Add("Sichtweite unter 1 km - Sichtflug nicht möglich");
            }
            else if (sichtweiteM < 3000)
            {
                hinweise.Add("Eingeschränkte Sicht");
            }

            // Wetterlage
            var schlechtesWetter = new[] { "rain", "snow", "sleet", "hail", "thunderstorm", "fog" };
            if (!string.IsNullOrEmpty(condition) && schlechtesWetter.Contains(condition.ToLowerInvariant()))
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

            // Flugkategorie-basierte Bewertung
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
            // Generiert einen METAR-ähnlichen String aus den BrightSky-Daten
            // Dies ist KEIN echter METAR, nur zur Anzeige
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

        public async Task<WeatherForecast?> GetForecastAsync(double latitude, double longitude)
        {
            try
            {
                var now = DateTime.UtcNow;
                var url = $"{BrightSkyBaseUrl}/weather?lat={latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&lon={longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&date={now:yyyy-MM-dd}";
                
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                    return null;

                var json = await response.Content.ReadAsStringAsync();
                var weatherResponse = JsonSerializer.Deserialize<BrightSkyWeatherResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (weatherResponse?.Weather == null || weatherResponse.Weather.Length == 0)
                    return null;

                var forecast = new WeatherForecast
                {
                    LetzteAktualisierung = DateTime.Now,
                    StundenVorhersage = new WeatherData[Math.Min(24, weatherResponse.Weather.Length)]
                };

                for (int i = 0; i < forecast.StundenVorhersage.Length && i < weatherResponse.Weather.Length; i++)
                {
                    forecast.StundenVorhersage[i] = MapToWeatherData(weatherResponse.Weather[i]);
                }

                return forecast;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private WeatherData MapToWeatherData(BrightSkyWeatherData data)
        {
            var windSpeed = data.GetWindSpeed();
            var windDir = data.GetWindDirection();
            
            var weather = new WeatherData
            {
                Zeitpunkt = data.Timestamp ?? DateTime.Now,
                Temperatur = data.Temperature ?? 0,
                Luftfeuchtigkeit = (int)(data.RelativeHumidity ?? 0),
                Windgeschwindigkeit = windSpeed,
                Windboeen = data.GetWindGustSpeed(),
                Windrichtung = (int)windDir,
                WindrichtungText = GetWindrichtungText((int)windDir),
                Niederschlag = data.GetPrecipitation(),
                Bewoelkung = (int)(data.CloudCover ?? 0),
                Sichtweite = (data.Visibility ?? 10000) / 1000.0, // m zu km
                Luftdruck = data.PressureMsl ?? 1013,
                Taupunkt = data.DewPoint ?? 0,
                Wetterlage = MapConditionToGerman(data.Condition),
                IstTag = data.Timestamp?.Hour >= 6 && data.Timestamp?.Hour < 20
            };

            // Gefuehlte Temperatur berechnen (vereinfacht)
            weather.GefuehlteTemperatur = CalculateFeelsLike(
                weather.Temperatur, 
                weather.Windgeschwindigkeit, 
                weather.Luftfeuchtigkeit);

            return weather;
        }

        private string GetWindrichtungText(int degrees)
        {
            if (degrees < 0) return "";
            
            var directions = new[] { "N", "NNO", "NO", "ONO", "O", "OSO", "SO", "SSO", 
                                     "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW" };
            var index = (int)Math.Round(degrees / 22.5) % 16;
            return directions[index];
        }

        private string MapConditionToGerman(string? condition)
        {
            if (string.IsNullOrEmpty(condition))
                return "Unbekannt";

            return condition.ToLowerInvariant() switch
            {
                "dry" => "Trocken",
                "fog" => "Nebel",
                "rain" => "Regen",
                "sleet" => "Schneeregen",
                "snow" => "Schnee",
                "hail" => "Hagel",
                "thunderstorm" => "Gewitter",
                "clear-day" => "Sonnig",
                "clear-night" => "Klar",
                "partly-cloudy-day" => "Teilweise bewoelkt",
                "partly-cloudy-night" => "Teilweise bewoelkt",
                "cloudy" => "Bewoelkt",
                "wind" => "Windig",
                _ => condition
            };
        }

        private double CalculateFeelsLike(double temp, double windSpeed, int humidity)
        {
            // Wind Chill fuer kalte Temperaturen
            if (temp <= 10 && windSpeed > 4.8)
            {
                var windChill = 13.12 + 0.6215 * temp - 11.37 * Math.Pow(windSpeed, 0.16) + 0.3965 * temp * Math.Pow(windSpeed, 0.16);
                return Math.Round(windChill, 1);
            }
            
            // Heat Index fuer warme Temperaturen
            if (temp >= 27 && humidity >= 40)
            {
                var hi = -8.784695 + 1.61139411 * temp + 2.338549 * humidity 
                         - 0.14611605 * temp * humidity - 0.012308094 * temp * temp 
                         - 0.016424828 * humidity * humidity;
                return Math.Round(hi, 1);
            }

            return temp;
        }

        #region BrightSky API Response Classes

        private class BrightSkyCurrentWeatherResponse
        {
            public BrightSkyWeatherData? Weather { get; set; }
            public BrightSkySource[]? Sources { get; set; }
        }

        private class BrightSkyWeatherResponse
        {
            public BrightSkyWeatherData[]? Weather { get; set; }
            public BrightSkySource[]? Sources { get; set; }
        }

        private class BrightSkyWeatherData
        {
            public DateTime? Timestamp { get; set; }
            public double? Temperature { get; set; }
            
            // BrightSky verwendet snake_case fuer Property-Namen
            [System.Text.Json.Serialization.JsonPropertyName("relative_humidity")]
            public double? RelativeHumidity { get; set; }
            
            // Current Weather API verwendet _10 suffix
            [System.Text.Json.Serialization.JsonPropertyName("wind_speed_10")]
            public double? WindSpeed10 { get; set; }
            
            // Forecast API verwendet ohne suffix
            [System.Text.Json.Serialization.JsonPropertyName("wind_speed")]
            public double? WindSpeed { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("wind_direction_10")]
            public double? WindDirection10 { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("wind_direction")]
            public double? WindDirection { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("wind_gust_speed_10")]
            public double? WindGustSpeed10 { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("wind_gust_speed")]
            public double? WindGustSpeed { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("precipitation_10")]
            public double? Precipitation10 { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("precipitation")]
            public double? Precipitation { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("cloud_cover")]
            public double? CloudCover { get; set; }
            
            public double? Visibility { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("pressure_msl")]
            public double? PressureMsl { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("dew_point")]
            public double? DewPoint { get; set; }
            
            public string? Condition { get; set; }
            public string? Icon { get; set; }
            
            // Helper properties um beide API-Versionen zu unterstuetzen
            public double GetWindSpeed() => WindSpeed10 ?? WindSpeed ?? 0;
            public double GetWindDirection() => WindDirection10 ?? WindDirection ?? 0;
            public double GetWindGustSpeed() => WindGustSpeed10 ?? WindGustSpeed ?? 0;
            public double GetPrecipitation() => Precipitation10 ?? Precipitation ?? 0;
        }

        private class BrightSkySource
        {
            public int? Id { get; set; }
            
            [System.Text.Json.Serialization.JsonPropertyName("station_name")]
            public string? StationName { get; set; }
        }

        #endregion
    }
}
