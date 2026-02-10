using AttackDash.Services;
using AttackDash.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddMemoryCache();

// Configure Loki service
var lokiUrl = builder.Configuration.GetValue<string>("Loki:BaseUrl") ?? "http://192.168.100.22:3100";
Console.WriteLine($"=== AttackDash starting ===");
Console.WriteLine($"=== Loki URL: {lokiUrl} ===");

builder.Services.AddHttpClient<LokiService>(client =>
{
    client.BaseAddress = new Uri(lokiUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHttpClient<StockService>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddHttpClient<WeatherService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Test endpoint to verify Loki connection
app.MapGet("/api/test-loki", async (LokiService loki) =>
{
    var stats = await loki.GetAttackStatsAsync();
    return Results.Ok(new
    {
        TotalCountries = stats.TotalCountries,
        AttacksLastHour = stats.AttacksLastHour,
        TopCountry = stats.TopCountry,
        Countries = stats.CountryBreakdown.Take(5).Select(c => new { c.Country, c.Count })
    });
});

app.Run();
