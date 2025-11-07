using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Alpaca.Markets;
using Environments = Alpaca.Markets.Environments;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RoaringBot
{
    public record AlgoRequest(string Symbol, int Short, int Long);

    public class Program
    {
        public static async Task Main(string[] args)
        {
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

            // ✅ Create Alpaca Data Client (Paper)
            var alpacaDataClient = Environments.Paper.GetAlpacaDataClient(new SecretKey(alpacaKey, alpacaSecret));
            builder.Services.AddSingleton<IAlpacaDataClient>(alpacaDataClient);

            var app = builder.Build();
            app.UseCors("AllowReactApp");

            Console.WriteLine("✅ C# backend is running inside Docker!");

            // --- Root test endpoint ---
            app.MapGet("/", () => "Hello from C# + Alpaca!");

            // --- Historical price endpoint ---
            app.MapGet("/stock/{symbol}/history", async (string symbol, IAlpacaDataClient client) =>
            {
                try
                {
                    var threeMonthsAgo = DateTime.UtcNow.AddMonths(-3);

                    var request = new HistoricalBarsRequest(symbol, threeMonthsAgo, DateTime.UtcNow, BarTimeFrame.Day)
                    {
                        Feed = MarketDataFeed.Iex // free IEX feed
                    };

                    var bars = await client.ListHistoricalBarsAsync(request);

                    var formattedHistory = bars.Items.Select(b => new
                    {
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
            app.MapGet("/stock/{symbol}/price", async (string symbol, IAlpacaDataClient client) =>
            {
                try
                {
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

            // --- Run Python SMA Algorithm ---
            app.MapPost("/run-algo", async (IAlpacaDataClient client, AlgoRequest request) =>
            {
                try
                {
                    Console.WriteLine($"[run-algo] Received request for {request.Symbol} (short={request.Short}, long={request.Long})");
                    var threeMonthsAgo = DateTime.UtcNow.AddMonths(-3);

                    var barsRequest = new HistoricalBarsRequest(request.Symbol, threeMonthsAgo, DateTime.UtcNow, BarTimeFrame.Day)
                    {
                        Feed = MarketDataFeed.Iex
                    };

                    var bars = await client.ListHistoricalBarsAsync(barsRequest);

                    var history = bars.Items.Select(b => new
                    {
                        date = b.TimeUtc.ToString("yyyy-MM-dd"),
                        close = b.Close
                    }).ToList();

                    var inputDict = new Dictionary<string, object>
                    {
                        ["symbol"] = request.Symbol,
                        ["short"] = request.Short,
                        ["long"] = request.Long,
                        ["bars"] = history
                    };

                    var jsonInput = JsonSerializer.Serialize(inputDict);
                    var scriptPath = Path.Combine("..", "algos-python", "algo_runner.py");

                    if (!File.Exists(scriptPath))
                    {
                        Console.Error.WriteLine($"Expected python script at: {Path.GetFullPath(scriptPath)}");
                        return Results.Problem($"Python script not found at path: {scriptPath}");
                    }

                    var psi = new ProcessStartInfo
                    {
                        FileName = "python3",
                        Arguments = scriptPath,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WorkingDirectory = Directory.GetCurrentDirectory()
                    };

                    using var process = new Process { StartInfo = psi };
                    process.Start();

                    await process.StandardInput.WriteAsync(jsonInput);
                    process.StandardInput.Close();

                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    process.WaitForExit();

                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        Console.Error.WriteLine("Python stderr:");
                        Console.Error.WriteLine(error);
                    }

                    if (string.IsNullOrWhiteSpace(output))
                    {
                        return Results.Problem("Python script returned no output.");
                    }

                    Console.WriteLine($"[run-algo] Python completed successfully for {request.Symbol}");

                    // ✅ Just return the Python output directly as JSON
                    return Results.Content(output, "application/json");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[run-algo] Exception: {ex}");
                    return Results.Problem($"Failed to run trading algorithm: {ex.Message}");
                }
            });


            await app.RunAsync();
        }
    }
}
