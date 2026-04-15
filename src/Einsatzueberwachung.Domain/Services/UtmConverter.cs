// UTM (Universal Transverse Mercator) ↔ Lat/Long Konvertierung
// Basierend auf dem WGS84-Ellipsoid

using System;

namespace Einsatzueberwachung.Domain.Services
{
    public static class UtmConverter
    {
        private const double A = 6378137.0;           // WGS84 semi-major axis
        private const double F = 1 / 298.257223563;   // WGS84 flattening
        private const double E = 0.0818191908426;      // WGS84 eccentricity
        private const double E2 = E * E;
        private const double K0 = 0.9996;              // UTM scale factor
        private const double FalseEasting = 500000.0;
        private const double FalseNorthingSouth = 10000000.0;

        /// <summary>
        /// Konvertiert Lat/Long (Dezimalgrad) zu UTM-Koordinaten
        /// </summary>
        public static (int Zone, char Band, double Easting, double Northing) LatLongToUtm(double latitude, double longitude)
        {
            int zone = (int)Math.Floor((longitude + 180.0) / 6.0) + 1;

            // Sonderregeln für Norwegen und Svalbard
            if (latitude >= 56 && latitude < 64 && longitude >= 3 && longitude < 12)
                zone = 32;
            if (latitude >= 72 && latitude < 84)
            {
                if (longitude >= 0 && longitude < 9) zone = 31;
                else if (longitude >= 9 && longitude < 21) zone = 33;
                else if (longitude >= 21 && longitude < 33) zone = 35;
                else if (longitude >= 33 && longitude < 42) zone = 37;
            }

            char band = GetUtmBand(latitude);
            double lonOrigin = (zone - 1) * 6 - 180 + 3; // Central meridian

            double latRad = latitude * Math.PI / 180.0;
            double lonRad = longitude * Math.PI / 180.0;
            double lonOriginRad = lonOrigin * Math.PI / 180.0;

            double ePrimeSquared = E2 / (1 - E2);
            double n = A / Math.Sqrt(1 - E2 * Math.Sin(latRad) * Math.Sin(latRad));
            double t = Math.Tan(latRad) * Math.Tan(latRad);
            double c = ePrimeSquared * Math.Cos(latRad) * Math.Cos(latRad);
            double a2 = Math.Cos(latRad) * (lonRad - lonOriginRad);

            double m = A * (
                (1 - E2 / 4 - 3 * E2 * E2 / 64 - 5 * E2 * E2 * E2 / 256) * latRad
                - (3 * E2 / 8 + 3 * E2 * E2 / 32 + 45 * E2 * E2 * E2 / 1024) * Math.Sin(2 * latRad)
                + (15 * E2 * E2 / 256 + 45 * E2 * E2 * E2 / 1024) * Math.Sin(4 * latRad)
                - (35 * E2 * E2 * E2 / 3072) * Math.Sin(6 * latRad)
            );

            double easting = K0 * n * (
                a2
                + (1 - t + c) * a2 * a2 * a2 / 6
                + (5 - 18 * t + t * t + 72 * c - 58 * ePrimeSquared) * a2 * a2 * a2 * a2 * a2 / 120
            ) + FalseEasting;

            double northing = K0 * (
                m + n * Math.Tan(latRad) * (
                    a2 * a2 / 2
                    + (5 - t + 9 * c + 4 * c * c) * a2 * a2 * a2 * a2 / 24
                    + (61 - 58 * t + t * t + 600 * c - 330 * ePrimeSquared) * a2 * a2 * a2 * a2 * a2 * a2 / 720
                )
            );

            if (latitude < 0)
                northing += FalseNorthingSouth;

            return (zone, band, Math.Round(easting, 2), Math.Round(northing, 2));
        }

        /// <summary>
        /// Konvertiert UTM-Koordinaten zu Lat/Long (Dezimalgrad)
        /// </summary>
        public static (double Latitude, double Longitude) UtmToLatLong(int zone, char band, double easting, double northing)
        {
            bool isNorthern = char.ToUpper(band) >= 'N';

            double x = easting - FalseEasting;
            double y = isNorthern ? northing : northing - FalseNorthingSouth;

            double lonOrigin = (zone - 1) * 6 - 180 + 3;

            double ePrimeSquared = E2 / (1 - E2);
            double m = y / K0;
            double mu = m / (A * (1 - E2 / 4 - 3 * E2 * E2 / 64 - 5 * E2 * E2 * E2 / 256));

            double e1 = (1 - Math.Sqrt(1 - E2)) / (1 + Math.Sqrt(1 - E2));
            double phi1 = mu
                + (3 * e1 / 2 - 27 * e1 * e1 * e1 / 32) * Math.Sin(2 * mu)
                + (21 * e1 * e1 / 16 - 55 * e1 * e1 * e1 * e1 / 32) * Math.Sin(4 * mu)
                + (151 * e1 * e1 * e1 / 96) * Math.Sin(6 * mu);

            double n1 = A / Math.Sqrt(1 - E2 * Math.Sin(phi1) * Math.Sin(phi1));
            double t1 = Math.Tan(phi1) * Math.Tan(phi1);
            double c1 = ePrimeSquared * Math.Cos(phi1) * Math.Cos(phi1);
            double r1 = A * (1 - E2) / Math.Pow(1 - E2 * Math.Sin(phi1) * Math.Sin(phi1), 1.5);
            double d = x / (n1 * K0);

            double latitude = phi1 - (n1 * Math.Tan(phi1) / r1) * (
                d * d / 2
                - (5 + 3 * t1 + 10 * c1 - 4 * c1 * c1 - 9 * ePrimeSquared) * d * d * d * d / 24
                + (61 + 90 * t1 + 298 * c1 + 45 * t1 * t1 - 252 * ePrimeSquared - 3 * c1 * c1) * d * d * d * d * d * d / 720
            );

            double longitude = (
                d
                - (1 + 2 * t1 + c1) * d * d * d / 6
                + (5 - 2 * c1 + 28 * t1 - 3 * c1 * c1 + 8 * ePrimeSquared + 24 * t1 * t1) * d * d * d * d * d / 120
            ) / Math.Cos(phi1);

            latitude = latitude * 180.0 / Math.PI;
            longitude = lonOrigin + longitude * 180.0 / Math.PI;

            return (Math.Round(latitude, 8), Math.Round(longitude, 8));
        }

        /// <summary>
        /// Versucht einen UTM-String zu parsen (z.B. "32U 461344 5481745" oder "32U461344E5481745N")
        /// </summary>
        public static bool TryParseUtm(string input, out int zone, out char band, out double easting, out double northing)
        {
            zone = 0;
            band = ' ';
            easting = 0;
            northing = 0;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            input = input.Trim().ToUpper();

            // Remove common separators
            input = input.Replace(",", " ").Replace(";", " ").Replace("/", " ");
            // Remove trailing E/N labels
            input = input.Replace("E ", " ").Replace("N ", " ");

            // Pattern: "32U 461344 5481745" or "32 U 461344 5481745"
            var parts = input.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 3)
            {
                // Try to extract zone and band from first part
                string zonePart = parts[0];
                string eastingPart;
                string northingPart;

                if (zonePart.Length >= 2 && char.IsLetter(zonePart[^1]))
                {
                    // "32U" format
                    if (!int.TryParse(zonePart[..^1], out zone))
                        return false;
                    band = zonePart[^1];
                    eastingPart = parts[1].TrimEnd('E', 'e');
                    northingPart = parts[2].TrimEnd('N', 'n');
                }
                else if (parts.Length >= 4 && int.TryParse(zonePart, out zone) && parts[1].Length == 1 && char.IsLetter(parts[1][0]))
                {
                    // "32 U 461344 5481745" format
                    band = parts[1][0];
                    eastingPart = parts[2].TrimEnd('E', 'e');
                    northingPart = parts[3].TrimEnd('N', 'n');
                }
                else
                {
                    return false;
                }

                if (zone < 1 || zone > 60)
                    return false;

                if (!IsValidUtmBand(band))
                    return false;

                if (!double.TryParse(eastingPart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out easting))
                    return false;

                if (!double.TryParse(northingPart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out northing))
                    return false;

                // Validate ranges
                if (easting < 100000 || easting > 900000)
                    return false;

                if (northing < 0 || northing > 10000000)
                    return false;

                return true;
            }

            return false;
        }

        /// <summary>
        /// Formatiert einen UTM-Zonenstring (z.B. "32U")
        /// </summary>
        public static string FormatUtmZone(int zone, char band) => $"{zone}{band}";

        private static char GetUtmBand(double latitude)
        {
            string bands = "CDEFGHJKLMNPQRSTUVWX";
            if (latitude < -80) return 'C';
            if (latitude >= 84) return 'X';
            int index = (int)Math.Floor((latitude + 80) / 8);
            if (index >= bands.Length) index = bands.Length - 1;
            return bands[index];
        }

        private static bool IsValidUtmBand(char band)
        {
            return "CDEFGHJKLMNPQRSTUVWX".Contains(char.ToUpper(band));
        }
    }
}
