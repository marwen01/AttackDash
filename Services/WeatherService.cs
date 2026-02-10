using System.Globalization;
using System.Text.Json;
using AttackDash.Models;
using Microsoft.Extensions.Caching.Memory;

namespace AttackDash.Services;

public class WeatherService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<WeatherService> _logger;

    // Täby, Stockholm coordinates
    private const double Latitude = 59.44;
    private const double Longitude = 18.07;

    public WeatherService(HttpClient httpClient, IMemoryCache cache, ILogger<WeatherService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<WeatherForecast> GetForecastAsync()
    {
        var cacheKey = "weather_forecast";
        if (_cache.TryGetValue(cacheKey, out WeatherForecast? cached))
            return cached!;

        var forecast = new WeatherForecast { LastUpdated = DateTime.Now };

        var smhiTask = FetchSmhiForecastAsync(forecast);
        var sunTask = FetchSunDataAsync(forecast);
        var extremesTask = FetchSwedenExtremesAsync(forecast);

        await Task.WhenAll(smhiTask, sunTask, extremesTask);

        _cache.Set(cacheKey, forecast, TimeSpan.FromMinutes(30));
        return forecast;
    }

    private async Task FetchSmhiForecastAsync(WeatherForecast forecast)
    {
        try
        {
            var url = $"https://opendata-download-metfcst.smhi.se/api/category/pmp3g/version/2/geotype/point/lon/{Longitude.ToString(CultureInfo.InvariantCulture)}/lat/{Latitude.ToString(CultureInfo.InvariantCulture)}/data.json";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("SMHI forecast returned {StatusCode}", response.StatusCode);
                return;
            }

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var timeSeries = doc.RootElement.GetProperty("timeSeries");

            var hourlyData = new List<HourlyForecast>();

            foreach (var entry in timeSeries.EnumerateArray())
            {
                var validTime = DateTime.Parse(entry.GetProperty("validTime").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                var parameters = entry.GetProperty("parameters");

                decimal temp = 0, wind = 0, precip = 0;
                int cloud = 0, weatherSymbol = 0;

                foreach (var param in parameters.EnumerateArray())
                {
                    var name = param.GetProperty("name").GetString();
                    var values = param.GetProperty("values");
                    switch (name)
                    {
                        case "t": temp = values[0].GetDecimal(); break;
                        case "ws": wind = values[0].GetDecimal(); break;
                        case "pmean": precip = values[0].GetDecimal(); break;
                        case "tcc_mean": cloud = (int)values[0].GetDecimal(); break;
                        case "r": // humidity
                            if (hourlyData.Count == 0)
                                forecast.Current.Humidity = (int)values[0].GetDecimal();
                            break;
                        case "Wsymb2": weatherSymbol = (int)values[0].GetDecimal(); break;
                    }
                }

                hourlyData.Add(new HourlyForecast
                {
                    Time = validTime.ToLocalTime(),
                    Temperature = temp,
                    Precipitation = precip,
                    WindSpeed = wind,
                    CloudCover = cloud
                });

                // Set current weather from first entry
                if (hourlyData.Count == 1)
                {
                    forecast.Current.Temperature = temp;
                    forecast.Current.WindSpeed = wind;
                    forecast.Current.Precipitation = precip;
                    forecast.Current.CloudCover = (int)(cloud / 8.0m * 100);
                    forecast.Current.Description = GetWeatherDescription(weatherSymbol);
                    forecast.Current.Icon = GetWeatherIcon(weatherSymbol);
                }
            }

            forecast.HourlyForecasts = hourlyData.Take(48).ToList();

            // Aggregate into daily forecasts
            var dailyGroups = hourlyData
                .GroupBy(h => h.Time.Date)
                .Take(7);

            foreach (var group in dailyGroups)
            {
                var items = group.ToList();
                forecast.DailyForecasts.Add(new DailyForecast
                {
                    Date = group.Key,
                    MinTemp = items.Min(h => h.Temperature),
                    MaxTemp = items.Max(h => h.Temperature),
                    TotalPrecipitation = items.Sum(h => h.Precipitation),
                    AvgWindSpeed = (int)items.Average(h => h.WindSpeed),
                    Icon = GetWeatherIcon(GetDominantWeather(items)),
                    Description = GetWeatherDescription(GetDominantWeather(items))
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching SMHI forecast");
        }
    }

    private async Task FetchSunDataAsync(WeatherForecast forecast)
    {
        try
        {
            var url = $"https://api.sunrise-sunset.org/json?lat={Latitude.ToString(CultureInfo.InvariantCulture)}&lng={Longitude.ToString(CultureInfo.InvariantCulture)}&formatted=0";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var results = doc.RootElement.GetProperty("results");

            forecast.Sun.Sunrise = DateTime.Parse(results.GetProperty("sunrise").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToLocalTime();
            forecast.Sun.Sunset = DateTime.Parse(results.GetProperty("sunset").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToLocalTime();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching sunrise/sunset data");
        }
    }

    private async Task FetchSwedenExtremesAsync(WeatherForecast forecast)
    {
        var cacheKey = "sweden_extremes";
        if (_cache.TryGetValue(cacheKey, out TemperatureExtremes? cached))
        {
            forecast.SwedenExtremes = cached;
            return;
        }

        try
        {
            var url = "https://opendata-download-metobs.smhi.se/api/version/1.0/parameter/1/station-set/all/period/latest-hour/data.json";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            var stations = doc.RootElement.GetProperty("station");
            var extremes = new TemperatureExtremes();
            decimal warmest = decimal.MinValue, coldest = decimal.MaxValue;

            foreach (var station in stations.EnumerateArray())
            {
                var stationName = station.GetProperty("name").GetString() ?? "";
                if (!station.TryGetProperty("value", out var values)) continue;
                var valueArray = values.EnumerateArray().ToList();
                if (valueArray.Count == 0) continue;

                var lastValue = valueArray.Last();
                if (!lastValue.TryGetProperty("value", out var tempStr)) continue;
                var tempString = tempStr.GetString();
                if (string.IsNullOrEmpty(tempString)) continue;
                if (!decimal.TryParse(tempString, NumberStyles.Any, CultureInfo.InvariantCulture, out var temp)) continue;

                if (temp > warmest)
                {
                    warmest = temp;
                    extremes.WarmestStation = stationName;
                    extremes.WarmestTemp = temp;
                }
                if (temp < coldest)
                {
                    coldest = temp;
                    extremes.ColdestStation = stationName;
                    extremes.ColdestTemp = temp;
                }
            }

            if (warmest != decimal.MinValue)
            {
                forecast.SwedenExtremes = extremes;
                _cache.Set(cacheKey, extremes, TimeSpan.FromHours(1));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Sweden temperature extremes");
        }
    }

    private static int GetDominantWeather(List<HourlyForecast> hours)
    {
        // Simple heuristic: if significant precipitation, it's rainy; else base on cloud cover
        if (hours.Any(h => h.Precipitation > 1)) return 8; // Rain
        var avgCloud = hours.Average(h => h.CloudCover);
        if (avgCloud > 6) return 6; // Overcast
        if (avgCloud > 3) return 3; // Partly cloudy
        return 1; // Clear
    }

    private static string GetWeatherDescription(int wsymb2)
    {
        return wsymb2 switch
        {
            1 => "Clear sky",
            2 => "Nearly clear",
            3 => "Partly cloudy",
            4 => "Partly cloudy",
            5 => "Cloudy",
            6 => "Overcast",
            7 => "Fog",
            8 => "Light rain",
            9 => "Moderate rain",
            10 => "Heavy rain",
            11 => "Thunderstorm",
            12 => "Light sleet",
            13 => "Moderate sleet",
            14 => "Heavy sleet",
            15 => "Light snow",
            16 => "Moderate snow",
            17 => "Heavy snow",
            18 => "Light rain",
            19 => "Moderate rain",
            20 => "Heavy rain",
            21 => "Thunder",
            22 => "Light sleet",
            23 => "Moderate sleet",
            24 => "Heavy sleet",
            25 => "Light snow",
            26 => "Moderate snow",
            27 => "Heavy snow",
            _ => "Unknown"
        };
    }

    private static string GetWeatherIcon(int wsymb2)
    {
        return wsymb2 switch
        {
            1 or 2 => "\u2600\ufe0f",      // sunny
            3 or 4 => "\u26c5",              // partly cloudy
            5 or 6 => "\u2601\ufe0f",        // cloudy
            7 => "\ud83c\udf2b\ufe0f",       // fog
            8 or 9 or 10 or 18 or 19 or 20 => "\ud83c\udf27\ufe0f", // rain
            11 or 21 => "\u26c8\ufe0f",      // thunder
            12 or 13 or 14 or 22 or 23 or 24 => "\ud83c\udf28\ufe0f", // sleet
            15 or 16 or 17 or 25 or 26 or 27 => "\u2744\ufe0f", // snow
            _ => "\ud83c\udf24\ufe0f"
        };
    }
}
