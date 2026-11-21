using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TensorStack.WPF.Controls;

namespace Amuse.App.Tools
{
    public sealed class WeatherTool : ToolCallBase
    {
        private const string GeocodingUrl = "https://geocoding-api.open-meteo.com/v1/search";
        private const string ForecastUrl = "https://api.open-meteo.com/v1/forecast";

        /// <summary>
        /// Gets the tool name.
        /// </summary>
        public override string Name => "weather";

        /// <summary>
        /// Gets the tool description.
        /// </summary>
        public override string Description => "Get the current weather and daily forecast for a location.";

        /// <summary>
        /// Gets the is default enabled.
        /// </summary>
        public override bool IsDefault => false;

        /// <summary>
        /// Gets the display name.
        /// </summary>
        public override string DisplayName => "Weather Tool";

        /// <summary>
        /// Gets the tool icon.
        /// </summary>
        public override string DisplayIcon => "f0c2";

        /// <summary>
        ///  Gets the tool icon style.
        /// </summary>
        public override FontAwesomeIconStyle DisplayIconStyle { get; } = FontAwesomeIconStyle.SharpLight;

        /// <summary>
        /// Gets the is display order.
        /// </summary>
        public override int DisplayOrder => 41;

        /// <summary>
        /// Gets the tool schema.
        /// </summary>
        public override string Schema => $$"""
        {
            "type": "function",
            "function": {
                "name": "{{Name}}",
                "description": "{{Description}} Use this tool for questions about current weather conditions, temperature, rain or the forecast for the coming days.",
                "parameters": {
                    "type": "object",
                    "properties": {
                        "location": {
                            "type": "string",
                            "description": "The city or place name, e.g. 'Auckland' or 'Paris, France'."
                        },
                        "days": {
                            "type": "integer",
                            "description": "The number of forecast days (1-7).",
                            "default": 3
                        },
                        "units": {
                            "type": "string",
                            "enum": ["metric", "imperial"],
                            "description": "The unit system: metric (°C, km/h, mm) or imperial (°F, mph, inch).",
                            "default": "metric"
                        }
                    },
                    "required": ["location"]
                }
            }
        }
        """;

        /// <summary>
        /// Executes the tool.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        public override async Task<string> ExecuteAsync(Settings settings, CancellationToken cancellationToken = default)
        {
            try
            {
                await CheckExternalAccess(settings);
                var location = GetArgument<string>("location");
                var days = Math.Clamp(GetArgumentOrDefault("days", 3), 1, 7);
                var isImperial = GetArgumentOrDefault("units", "metric").Equals("imperial", StringComparison.OrdinalIgnoreCase);

                // Geocode
                var place = await GeocodeAsync(location, cancellationToken);

                // Forecast
                var forecastUrl = string.Create(CultureInfo.InvariantCulture, $"{ForecastUrl}?latitude={place.Latitude}&longitude={place.Longitude}&timezone=auto&forecast_days={days}")
                    + "&current=temperature_2m,apparent_temperature,relative_humidity_2m,weather_code,wind_speed_10m,precipitation"
                    + "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max,precipitation_sum"
                    + (isImperial ? "&temperature_unit=fahrenheit&wind_speed_unit=mph&precipitation_unit=inch" : string.Empty);

                using (var document = JsonDocument.Parse(await GetStringAsync(forecastUrl, cancellationToken)))
                {
                    var root = document.RootElement;
                    var current = root.GetProperty("current");
                    var currentResult = new CurrentWeather
                    (
                        current.GetProperty("time").GetString(),
                        GetCondition(GetInt(current, "weather_code")),
                        GetDouble(current, "temperature_2m"),
                        GetDouble(current, "apparent_temperature"),
                        GetInt(current, "relative_humidity_2m"),
                        GetDouble(current, "wind_speed_10m"),
                        GetDouble(current, "precipitation")
                    );

                    var daily = root.GetProperty("daily");
                    var dates = daily.GetProperty("time");
                    var dailyResults = new List<DailyWeather>();
                    for (int i = 0; i < dates.GetArrayLength(); i++)
                    {
                        var date = dates[i].GetString();
                        var dayName = DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime) ? dateTime.ToString("dddd", CultureInfo.InvariantCulture) : null;
                        dailyResults.Add(new DailyWeather
                        (
                            date,
                            dayName,
                            GetCondition(GetInt(daily, "weather_code", i)),
                            GetDouble(daily, "temperature_2m_min", i),
                            GetDouble(daily, "temperature_2m_max", i),
                            GetInt(daily, "precipitation_probability_max", i),
                            GetDouble(daily, "precipitation_sum", i)
                        ));
                    }

                    var units = isImperial
                        ? new WeatherUnits("°F", "mph", "inch")
                        : new WeatherUnits("°C", "km/h", "mm");
                    var result = new WeatherResult(place.Name, place.Region, place.Country, units, currentResult, dailyResults);
                    return SuccessResult(result);
                }
            }
            catch (Exception ex)
            {
                return ErrorResult(ex);
            }
        }


        /// <summary>
        /// Geocodes the location name.
        /// </summary>
        /// <param name="location">The location.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        private async Task<Place> GeocodeAsync(string location, CancellationToken cancellationToken)
        {
            // Use the city part for lookup, keep the rest to prefer a matching country/region
            var parts = location.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
                throw new ArgumentException("A location is required.");

            var url = $"{GeocodingUrl}?name={Uri.EscapeDataString(parts[0])}&count=10&language=en&format=json";
            using (var document = JsonDocument.Parse(await GetStringAsync(url, cancellationToken)))
            {
                if (!document.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                    throw new Exception($"Location '{location}' not found.");

                var match = results[0];
                if (parts.Length > 1)
                {
                    var qualifier = parts[1];
                    foreach (var result in results.EnumerateArray())
                    {
                        if (GetString(result, "country").Contains(qualifier, StringComparison.OrdinalIgnoreCase)
                         || GetString(result, "country_code").Equals(qualifier, StringComparison.OrdinalIgnoreCase)
                         || GetString(result, "admin1").Contains(qualifier, StringComparison.OrdinalIgnoreCase))
                        {
                            match = result;
                            break;
                        }
                    }
                }

                return new Place
                (
                    GetString(match, "name"),
                    GetString(match, "admin1"),
                    GetString(match, "country"),
                    match.GetProperty("latitude").GetDouble(),
                    match.GetProperty("longitude").GetDouble()
                );
            }
        }


        /// <summary>
        /// Gets the condition text for a WMO weather code.
        /// </summary>
        /// <param name="code">The code.</param>
        private static string GetCondition(int code)
        {
            return code switch
            {
                0 => "Clear sky",
                1 => "Mainly clear",
                2 => "Partly cloudy",
                3 => "Overcast",
                45 or 48 => "Fog",
                51 or 53 or 55 => "Drizzle",
                56 or 57 => "Freezing drizzle",
                61 => "Light rain",
                63 => "Rain",
                65 => "Heavy rain",
                66 or 67 => "Freezing rain",
                71 => "Light snow",
                73 => "Snow",
                75 => "Heavy snow",
                77 => "Snow grains",
                80 or 81 => "Rain showers",
                82 => "Violent rain showers",
                85 or 86 => "Snow showers",
                95 => "Thunderstorm",
                96 or 99 => "Thunderstorm with hail",
                _ => "Unknown"
            };
        }


        /// <summary>
        /// Gets a string property, or empty.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="name">The name.</param>
        private static string GetString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : string.Empty;
        }


        /// <summary>
        /// Gets a number property, or array item when an index is specified.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="name">The name.</param>
        /// <param name="index">The array index.</param>
        private static double GetDouble(JsonElement element, string name, int index = -1)
        {
            if (!element.TryGetProperty(name, out var value))
                return 0;
            if (index >= 0)
                value = value[index];
            return value.ValueKind == JsonValueKind.Number ? Math.Round(value.GetDouble(), 1) : 0;
        }


        /// <summary>
        /// Gets an integer property, or array item when an index is specified.
        /// </summary>
        /// <param name="element">The element.</param>
        /// <param name="name">The name.</param>
        /// <param name="index">The array index.</param>
        private static int GetInt(JsonElement element, string name, int index = -1)
        {
            return (int)Math.Round(GetDouble(element, name, index));
        }


        /// <summary>
        /// Place structure.
        /// </summary>
        private sealed record Place(string Name, string Region, string Country, double Latitude, double Longitude);


        /// <summary>
        /// WeatherUnits structure.
        /// </summary>
        private sealed record WeatherUnits(string Temperature, string WindSpeed, string Precipitation);


        /// <summary>
        /// CurrentWeather structure.
        /// </summary>
        private sealed record CurrentWeather(string Time, string Condition, double Temperature, double FeelsLike, int Humidity, double WindSpeed, double Precipitation);


        /// <summary>
        /// DailyWeather structure.
        /// </summary>
        private sealed record DailyWeather(string Date, string Day, string Condition, double Min, double Max, int PrecipitationChance, double Precipitation);


        /// <summary>
        /// WeatherResult structure.
        /// </summary>
        private sealed record WeatherResult(string Location, string Region, string Country, WeatherUnits Units, CurrentWeather Current, List<DailyWeather> Daily);
    }
}
