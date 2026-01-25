namespace AttackDash.Models;

public class AttackData
{
    public string Country { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Count { get; set; }
    public string? SourceIp { get; set; }
    public DateTime Timestamp { get; set; }
    public string? RawLog { get; set; }
}

public class AttackStats
{
    public int TotalAttacks { get; set; }
    public int TotalCountries { get; set; }
    public int AttacksLastHour { get; set; }
    public int AttacksPreviousHour { get; set; }
    public double TrendPercentage => AttacksPreviousHour > 0
        ? ((AttacksLastHour - AttacksPreviousHour) / (double)AttacksPreviousHour) * 100
        : 0;
    public bool TrendUp => AttacksLastHour > AttacksPreviousHour;
    public string TopCountry { get; set; } = string.Empty;
    public int TopCountryCount { get; set; }
    public List<CountryAttackCount> CountryBreakdown { get; set; } = new();
    public List<CountryAttackCount> MapMarkers { get; set; } = new();  // City-level for map
}

public class CountryAttackCount
{
    public string Country { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class RecentAttack
{
    public DateTime Timestamp { get; set; }
    public string Country { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string? SourceIp { get; set; }
    public int DestinationPort { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
