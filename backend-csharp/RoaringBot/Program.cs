using Alpaca.Markets;
using Environments = Alpaca.Markets.Environments;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5075");

var app = builder.Build();

Console.WriteLine("C# backend is running inside Docker!");

// Alpaca credentials from env vars
var alpacaKey = Environment.GetEnvironmentVariable("ALPACA_KEY");
var alpacaSecret = Environment.GetEnvironmentVariable("ALPACA_SECRET");

// Create Alpaca client
var alpacaClient = Environments.Paper
    .GetAlpacaDataClient(new SecretKey(alpacaKey, alpacaSecret));

// Optional: fetch and log last trade for AAPL
var trade = await alpacaClient.GetLatestTradeAsync(new LatestMarketDataRequest("AAPL"));
Console.WriteLine($"AAPL Last Trade: {trade.Price} at {trade.TimestampUtc}");

// Minimal API test route
app.MapGet("/", () => "Hello from C# + Alpaca!");

// Route to fetch latest trade for any symbol
app.MapGet("/latest-trade/{symbol}", async (string symbol) =>
{
    try
    {
        var latest = await alpacaClient.GetLatestTradeAsync(new LatestMarketDataRequest(symbol));
        return Results.Ok(new
        {
            Symbol = symbol,
            Price = latest.Price,
            Timestamp = latest.TimestampUtc
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Failed to fetch trade: {ex.Message}");
    }
});

app.Run();
