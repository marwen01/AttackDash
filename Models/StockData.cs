namespace AttackDash.Models;

public class StockQuote
{
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal PreviousClose { get; set; }
    public decimal Change => Price - PreviousClose;
    public decimal ChangePercent => PreviousClose != 0 ? (Change / PreviousClose) * 100 : 0;
    public bool IsPositive => Change >= 0;
    public string Currency { get; set; } = "SEK";
    public DateTime LastUpdated { get; set; }
    public List<StockCandle> IntradayData { get; set; } = new();
}

public class StockCandle
{
    public DateTime Timestamp { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}
