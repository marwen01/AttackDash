using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using AttackDash.Models;

namespace AttackDash.Services;

public class LokiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LokiService> _logger;

    public LokiService(HttpClient httpClient, ILogger<LokiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<AttackStats> GetAttackStatsAsync()
    {
        var stats = new AttackStats();

        try
        {
            // Get country breakdown for last hour
            var countryData = await QueryLokiAsync(
                "sum by (country, country_code, latitude, longitude) (count_over_time({job=\"unifi_wan\"} [1h]))"
            );

            var cityBreakdown = ParseCountryBreakdown(countryData);
            // Keep city-level data for map markers
            stats.MapMarkers = cityBreakdown.OrderByDescending(c => c.Count).ToList();
            // Aggregate by country for the list (different lat/long for same country creates duplicates)
            var countryBreakdown = AggregateByCountry(cityBreakdown);
            stats.CountryBreakdown = countryBreakdown.OrderByDescending(c => c.Count).ToList();
            stats.TotalCountries = countryBreakdown.Count;
            stats.AttacksLastHour = countryBreakdown.Sum(c => c.Count);

            if (countryBreakdown.Any())
            {
                var top = countryBreakdown.OrderByDescending(c => c.Count).First();
                stats.TopCountry = top.Country;
                stats.TopCountryCount = top.Count;
            }

            // Get previous hour for trend
            var previousHourData = await QueryLokiAsync(
                "sum(count_over_time({job=\"unifi_wan\"} [1h] offset 1h))"
            );
            stats.AttacksPreviousHour = ParseTotalCount(previousHourData);

            // Get total attacks (last 24h)
            var totalData = await QueryLokiAsync(
                "sum(count_over_time({job=\"unifi_wan\"} [24h]))"
            );
            stats.TotalAttacks = ParseTotalCount(totalData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching attack stats from Loki");
        }

        return stats;
    }

    public async Task<List<RecentAttack>> GetRecentAttacksAsync(int limit = 10)
    {
        var attacks = new List<RecentAttack>();

        try
        {
            var data = await QueryRangeAsync("{job=\"unifi_wan\"}", limit);
            attacks = ParseRecentAttacks(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching recent attacks from Loki");
        }

        return attacks;
    }

    public async Task<List<CountryAttackCount>> GetAttacksByCountryAsync(string timeRange = "1h")
    {
        try
        {
            var data = await QueryLokiAsync(
                $"sum by (country, country_code, latitude, longitude) (count_over_time({{job=\"unifi_wan\"}} [{timeRange}]))"
            );
            var breakdown = ParseCountryBreakdown(data);
            return AggregateByCountry(breakdown).OrderByDescending(c => c.Count).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching attacks by country from Loki");
            return new List<CountryAttackCount>();
        }
    }

    private async Task<JsonDocument?> QueryLokiAsync(string query)
    {
        var encodedQuery = HttpUtility.UrlEncode(query);
        var url = $"/loki/api/v1/query?query={encodedQuery}";

        _logger.LogInformation("Querying Loki: {BaseUrl}{Url}", _httpClient.BaseAddress, url);

        try
        {
            var response = await _httpClient.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Loki response: {StatusCode}, Content length: {Length}",
                response.StatusCode, content.Length);

            if (response.IsSuccessStatusCode)
            {
                return JsonDocument.Parse(content);
            }

            _logger.LogWarning("Loki query failed: {StatusCode}, Response: {Content}",
                response.StatusCode, content.Substring(0, Math.Min(200, content.Length)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception querying Loki at {BaseUrl}", _httpClient.BaseAddress);
        }

        return null;
    }

    private async Task<JsonDocument?> QueryRangeAsync(string query, int limit)
    {
        var encodedQuery = HttpUtility.UrlEncode(query);
        var url = $"/loki/api/v1/query_range?query={encodedQuery}&limit={limit}";

        var response = await _httpClient.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            return JsonDocument.Parse(content);
        }

        _logger.LogWarning("Loki query_range failed: {StatusCode}", response.StatusCode);
        return null;
    }

    private List<CountryAttackCount> AggregateByCountry(List<CountryAttackCount> data)
    {
        return data
            .GroupBy(c => c.CountryCode)
            .Select(g => new CountryAttackCount
            {
                Country = g.First().Country,
                CountryCode = g.Key,
                Count = g.Sum(x => x.Count),
                // Use the lat/long from the entry with the highest count (most representative)
                Latitude = g.OrderByDescending(x => x.Count).First().Latitude,
                Longitude = g.OrderByDescending(x => x.Count).First().Longitude
            })
            .ToList();
    }

    private List<CountryAttackCount> ParseCountryBreakdown(JsonDocument? doc)
    {
        var result = new List<CountryAttackCount>();
        if (doc == null) return result;

        try
        {
            var results = doc.RootElement
                .GetProperty("data")
                .GetProperty("result");

            foreach (var item in results.EnumerateArray())
            {
                var metric = item.GetProperty("metric");
                var value = item.GetProperty("value")[1].GetString();

                var country = metric.TryGetProperty("country", out var c) ? c.GetString() ?? "Unknown" : "Unknown";
                var countryCode = metric.TryGetProperty("country_code", out var cc) ? cc.GetString() ?? "XX" : "XX";
                var lat = metric.TryGetProperty("latitude", out var latProp) ? double.Parse(latProp.GetString() ?? "0", CultureInfo.InvariantCulture) : 0;
                var lon = metric.TryGetProperty("longitude", out var lonProp) ? double.Parse(lonProp.GetString() ?? "0", CultureInfo.InvariantCulture) : 0;

                result.Add(new CountryAttackCount
                {
                    Country = country.Replace("_", " "),
                    CountryCode = countryCode,
                    Count = int.Parse(value ?? "0"),
                    Latitude = lat,
                    Longitude = lon
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing country breakdown");
        }

        return result;
    }

    private int ParseTotalCount(JsonDocument? doc)
    {
        if (doc == null) return 0;

        try
        {
            var results = doc.RootElement
                .GetProperty("data")
                .GetProperty("result");

            if (results.GetArrayLength() > 0)
            {
                var value = results[0].GetProperty("value")[1].GetString();
                return int.Parse(value ?? "0");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing total count");
        }

        return 0;
    }

    private List<RecentAttack> ParseRecentAttacks(JsonDocument? doc)
    {
        var result = new List<RecentAttack>();
        if (doc == null) return result;

        try
        {
            var results = doc.RootElement
                .GetProperty("data")
                .GetProperty("result");

            foreach (var stream in results.EnumerateArray())
            {
                var labels = stream.GetProperty("stream");
                var country = labels.TryGetProperty("country", out var c) ? c.GetString() ?? "Unknown" : "Unknown";
                var countryCode = labels.TryGetProperty("country_code", out var cc) ? cc.GetString() ?? "XX" : "XX";
                var lat = labels.TryGetProperty("latitude", out var latProp) ? double.Parse(latProp.GetString() ?? "0", CultureInfo.InvariantCulture) : 0;
                var lon = labels.TryGetProperty("longitude", out var lonProp) ? double.Parse(lonProp.GetString() ?? "0", CultureInfo.InvariantCulture) : 0;

                var values = stream.GetProperty("values");
                foreach (var val in values.EnumerateArray())
                {
                    var timestamp = long.Parse(val[0].GetString() ?? "0");
                    var logLine = val[1].GetString() ?? "";

                    // Extract source IP from log line
                    var srcIpMatch = Regex.Match(logLine, @"SRC=(\d+\.\d+\.\d+\.\d+)");
                    var dptMatch = Regex.Match(logLine, @"DPT=(\d+)");

                    result.Add(new RecentAttack
                    {
                        Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(timestamp / 1_000_000).DateTime,
                        Country = country.Replace("_", " "),
                        CountryCode = countryCode,
                        SourceIp = srcIpMatch.Success ? srcIpMatch.Groups[1].Value : null,
                        DestinationPort = dptMatch.Success ? int.Parse(dptMatch.Groups[1].Value) : 0,
                        Latitude = lat,
                        Longitude = lon
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing recent attacks");
        }

        return result.OrderByDescending(a => a.Timestamp).ToList();
    }
}
