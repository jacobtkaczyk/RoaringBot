using Alpaca.Markets;
using Environments = Alpaca.Markets.Environments;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://0.0.0.0:5075");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

var alpacaKey = Environment.GetEnvironmentVariable("ALPACA_KEY");
var alpacaSecret = Environment.GetEnvironmentVariable("ALPACA_SECRET");

if (string.IsNullOrEmpty(alpacaKey) || string.IsNullOrEmpty(alpacaSecret))
{
    throw new InvalidOperationException("Alpaca API keys are not configured. Check your .env file.");
}

// --- ✅ Updated client creation for Alpaca.Markets v5.x ---
var alpacaDataClient = Environments.Paper.GetAlpacaDataClient(new SecretKey(alpacaKey, alpacaSecret));

// Register with dependency injection
builder.Services.AddSingleton<IAlpacaDataClient>(alpacaDataClient);

var app = builder.Build();
app.UseCors("AllowReactApp");

Console.WriteLine("C# backend is running inside Docker!");

// Root test endpoint
app.MapGet("/", () => "Hello from C# + Alpaca!");

// --- Historical price endpoint ---
app.MapGet("/stock/{symbol}/history",
    async (string symbol, IAlpacaDataClient client) =>
{
    try
    {
        var threeMonthsAgo = DateTime.UtcNow.AddMonths(-3);

        // ✅ In v5, Feed is specified per request
        var request = new HistoricalBarsRequest(symbol, threeMonthsAgo, DateTime.UtcNow, BarTimeFrame.Day)
        {
            Feed = MarketDataFeed.Iex // free IEX feed
        };

        var bars = await client.ListHistoricalBarsAsync(request);

        var formattedHistory = bars.Items.Select(b => new {
            date = b.TimeUtc.ToString("yyyy-MM-dd"),
            price = b.Close
        }).ToList();

        return Results.Ok(formattedHistory);
    }
    catch (RestClientErrorException ex)
    {
        return ex.ErrorCode switch
        {
            401 => Results.Problem(statusCode: 401, detail: "Authentication failed. Check your API keys."),
            403 => Results.Problem(statusCode: 403, detail: "Your API keys do not have permission for this request."),
            422 => Results.NotFound($"Symbol '{symbol}' not found."),
            429 => Results.Problem(statusCode: 429, detail: "Rate limit exceeded. Please try again later."),
            _ => Results.Problem(statusCode: 500, detail: $"An Alpaca API error occurred: {ex.Message}")
        };
    }
    catch (Exception ex)
    {
        return Results.Problem($"An unexpected error occurred: {ex.Message}");
    }
});

// --- Latest price endpoint ---
app.MapGet("/stock/{symbol}/price",
    async (string symbol, IAlpacaDataClient client) =>
{
    try
    {
        // ✅ Use LatestMarketDataRequest with Feed = Iex
        var request = new LatestMarketDataRequest(symbol)
        {
            Feed = MarketDataFeed.Iex
        };

        var quote = await client.GetLatestQuoteAsync(request);

        return Results.Ok(new { symbol, price = quote.AskPrice });
    }
    catch (RestClientErrorException ex)
    {
        return ex.ErrorCode == 422
            ? Results.NotFound($"Symbol '{symbol}' not found.")
            : Results.Problem($"An Alpaca API error occurred: {ex.Message}");
    }
    catch (Exception ex)
    {
        return Results.Problem($"An unexpected error occurred: {ex.Message}");
    }
});

app.Run();

