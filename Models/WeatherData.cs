namespace AttackDash.Models;

public class WeatherForecast
{
    public CurrentWeather Current { get; set; } = new();
    public List<DailyForecast> DailyForecasts { get; set; } = new();
    public List<HourlyForecast> HourlyForecasts { get; set; } = new();
    public SunData Sun { get; set; } = new();
    public TemperatureExtremes? SwedenExtremes { get; set; }
    public DateTime LastUpdated { get; set; }
}

public class CurrentWeather
{
    public decimal Temperature { get; set; }
    public decimal WindSpeed { get; set; }
    public int Humidity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public decimal Precipitation { get; set; }
    public int CloudCover { get; set; }
}

public class DailyForecast
{
    public DateTime Date { get; set; }
    public decimal MinTemp { get; set; }
    public decimal MaxTemp { get; set; }
    public decimal TotalPrecipitation { get; set; }
    public string Icon { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int AvgWindSpeed { get; set; }
}

public class HourlyForecast
{
    public DateTime Time { get; set; }
    public decimal Temperature { get; set; }
    public decimal Precipitation { get; set; }
    public decimal WindSpeed { get; set; }
    public int CloudCover { get; set; }
}

public class SunData
{
    public DateTime Sunrise { get; set; }
    public DateTime Sunset { get; set; }
    public TimeSpan DayLength => Sunset - Sunrise;
}

public class TemperatureExtremes
{
    public string WarmestStation { get; set; } = string.Empty;
    public decimal WarmestTemp { get; set; }
    public string ColdestStation { get; set; } = string.Empty;
    public decimal ColdestTemp { get; set; }
}
