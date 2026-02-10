using System.Globalization;
using System.Text.Json;
using AttackDash.Models;
using Microsoft.Extensions.Caching.Memory;

namespace AttackDash.Services;

public class StockService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<StockService> _logger;

    private static readonly Dictionary<string, string> Indices = new()
    {
        { "^GDAXI", "DAX" },
        { "^OMX", "OMX30" },
        { "^GSPC", "S&P 500" },
        { "^IXIC", "NASDAQ" }
    };

    private static readonly Dictionary<string, string> Stocks = new()
    {
        { "ELUX-B.ST", "Electrolux B" },
        { "VOLCAR-B.ST", "Volvo Cars B" },
        { "ERIC-B.ST", "Ericsson B" },
        { "HM-B.ST", "H&M B" },
        { "ATCO-A.ST", "Atlas Copco A" }
    };

    public StockService(HttpClient httpClient, IMemoryCache cache, ILogger<StockService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<StockQuote>> GetIndicesAsync()
    {
        var tasks = Indices.Select(kvp => GetQuoteAsync(kvp.Key, kvp.Value));
        var results = await Task.WhenAll(tasks);
        return results.Where(q => q != null).Cast<StockQuote>().ToList();
    }

    public async Task<List<StockQuote>> GetStocksAsync()
    {
        var tasks = Stocks.Select(kvp => GetQuoteAsync(kvp.Key, kvp.Value));
        var results = await Task.WhenAll(tasks);
        return results.Where(q => q != null).Cast<StockQuote>().ToList();
    }

    public async Task<StockQuote?> GetQuoteAsync(string symbol, string name)
    {
        var cacheKey = $"stock_{symbol}";
        if (_cache.TryGetValue(cacheKey, out StockQuote? cached))
            return cached;

        try
        {
            var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol}?range=1d&interval=5m";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Yahoo Finance returned {StatusCode} for {Symbol}", response.StatusCode, symbol);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            var result = doc.RootElement
                .GetProperty("chart")
                .GetProperty("result")[0];

            var meta = result.GetProperty("meta");
            var price = meta.GetProperty("regularMarketPrice").GetDecimal();
            var previousClose = meta.GetProperty("chartPreviousClose").GetDecimal();
            var currency = meta.TryGetProperty("currency", out var curr) ? curr.GetString() ?? "USD" : "USD";

            var quote = new StockQuote
            {
                Symbol = symbol,
                Name = name,
                Price = price,
                PreviousClose = previousClose,
                Currency = currency,
                LastUpdated = DateTime.Now
            };

            // Parse intraday candle data
            if (result.TryGetProperty("timestamp", out var timestamps) &&
                result.TryGetProperty("indicators", out var indicators))
            {
                var quoteData = indicators.GetProperty("quote")[0];
                var opens = quoteData.GetProperty("open");
                var highs = quoteData.GetProperty("high");
                var lows = quoteData.GetProperty("low");
                var closes = quoteData.GetProperty("close");
                var volumes = quoteData.GetProperty("volume");

                for (int i = 0; i < timestamps.GetArrayLength(); i++)
                {
                    var ts = timestamps[i].GetInt64();
                    var closeVal = closes[i];
                    if (closeVal.ValueKind == JsonValueKind.Null) continue;

                    quote.IntradayData.Add(new StockCandle
                    {
                        Timestamp = DateTimeOffset.FromUnixTimeSeconds(ts).LocalDateTime,
                        Open = opens[i].ValueKind != JsonValueKind.Null ? opens[i].GetDecimal() : 0,
                        High = highs[i].ValueKind != JsonValueKind.Null ? highs[i].GetDecimal() : 0,
                        Low = lows[i].ValueKind != JsonValueKind.Null ? lows[i].GetDecimal() : 0,
                        Close = closeVal.GetDecimal(),
                        Volume = volumes[i].ValueKind != JsonValueKind.Null ? volumes[i].GetInt64() : 0
                    });
                }
            }

            _cache.Set(cacheKey, quote, TimeSpan.FromMinutes(5));
            return quote;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching stock data for {Symbol}", symbol);
            return null;
        }
    }
}
