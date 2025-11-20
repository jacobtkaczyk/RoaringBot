using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Alpaca.Markets;
using Environments = Alpaca.Markets.Environments;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RoaringBot
{
    public record AlgoRequest(string Symbol, int Short, int Long);
    public record TradeRequest(string Symbol, int Short, int Long, decimal Quantity = 1);

    // New request records for added endpoints
    public record RunSelectedAlgoRequest(string Algo, string Symbol, int Short, int Long);
    public record ValidateTickerRequest(string Symbol);

    public static class LogStream
    {
        private static readonly List<HttpResponse> _clients = new();
        private static readonly object _lock = new();

        public static void AddClient(HttpResponse response)
        {
            lock (_lock)
            {
                _clients.Add(response);
            }
        }

        public static void RemoveClient(HttpResponse response)
        {
            lock (_lock)
            {
                _clients.Remove(response);
            }
        }

        public static async Task PushAsync(string message)
        {
            List<HttpResponse> clientsCopy;

            lock (_lock)
            {
                clientsCopy = _clients.ToList();
            }

            foreach (var client in clientsCopy)
            {
                try
                {
                    await client.WriteAsync($"data: {message}\n\n");
                    await client.Body.FlushAsync();
                }
                catch
                {
                    RemoveClient(client);
                }
            }
        }
    }

    public class Program
    {
        private static CancellationTokenSource? _runCts = null;
        private static bool _isRunning = false;

        public record AlgoSignalResult(string Signal);

        public record RunSelectedAlgoStartRequest(
            string Algo,
            string Symbol,
            int Short,
            int Long,
            int DurationMinutes,
            int IntervalSeconds
        );

        private static async Task<AlgoSignalResult> RunAlgoOnce(
     IAlpacaDataClient client,
     RunSelectedAlgoStartRequest req)
        {
            var threeMonthsAgo = DateTime.UtcNow.AddMonths(-3);

            var barsRequest = new HistoricalBarsRequest(
                req.Symbol,
                threeMonthsAgo,
                DateTime.UtcNow,
                BarTimeFrame.Day)
            {
                Feed = MarketDataFeed.Iex
            };

            var bars = await client.ListHistoricalBarsAsync(barsRequest);

            var history = bars.Items
                .Select(b => new { date = b.TimeUtc.ToString("yyyy-MM-dd"), close = b.Close })
                .ToList();

            var inputDict = new Dictionary<string, object>
            {
                ["symbol"] = req.Symbol,
                ["short"] = req.Short,
                ["long"] = req.Long,
                ["bars"] = history
            };

            var jsonInput = JsonSerializer.Serialize(inputDict);

            var scriptPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "algos-python",
                $"{req.Algo}.py"
            );

            var psi = new ProcessStartInfo
            {
                FileName = "python3",
                Arguments = $"\"{scriptPath}\"",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };

            using var process = new Process { StartInfo = psi };
            process.Start();

            // Send JSON to Python
            await process.StandardInput.WriteAsync(jsonInput);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();

            // Read output
            var output = await process.StandardOutput.ReadToEndAsync();
            var errors = await process.StandardError.ReadToEndAsync();

            process.WaitForExit();

            if (!string.IsNullOrWhiteSpace(errors))
                Console.WriteLine($"[Algo Python Error]: {errors}");

            var signal = output
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? "HOLD";

            return new AlgoSignalResult(signal.ToUpper());
        }



        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.WebHost.UseUrls("http://0.0.0.0:5075");

            // ✅ CORS setup — includes React dev ports + Docker bridge
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowReactApp", policy =>
                {
                    policy.WithOrigins(
                        "http://localhost:3000",
                        "http://localhost:5173",
                        "http://127.0.0.1:5173",
                        "http://frontend:5173" // Docker service name if you compose them
                    )
                    .AllowAnyMethod()
                    .AllowAnyHeader();
                });
            });

            // ✅ Load API keys from environment
            var alpacaKey = Environment.GetEnvironmentVariable("ALPACA_KEY");
            var alpacaSecret = Environment.GetEnvironmentVariable("ALPACA_SECRET");

            if (string.IsNullOrEmpty(alpacaKey) || string.IsNullOrEmpty(alpacaSecret))
            {
                throw new InvalidOperationException("Alpaca API keys are not configured. Check your .env file or Docker env variables.");
            }

            // ✅ Create Alpaca Data Client (Paper)
            var secretKey = new SecretKey(alpacaKey, alpacaSecret);
            var alpacaDataClient = Environments.Paper.GetAlpacaDataClient(secretKey);
            var alpacaTradingClient = Environments.Paper.GetAlpacaTradingClient(secretKey);
            builder.Services.AddSingleton<IAlpacaDataClient>(alpacaDataClient);
            builder.Services.AddSingleton<IAlpacaTradingClient>(alpacaTradingClient);

            builder.Services.AddControllers();

            var app = builder.Build();
            app.UseCors("AllowReactApp");
            app.MapControllers();

            Console.WriteLine("✅ C# backend is running inside Docker!");
            Console.WriteLine($"🌎 Listening on: http://0.0.0.0:5075");
            Console.WriteLine($"🧠 Environment: {builder.Environment.EnvironmentName}");

            // --- Health check endpoint ---
            app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "RoaringBot.CSharp" }));

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
                        Feed = MarketDataFeed.Iex
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
                        _ => Results.Problem(statusCode: 500, detail: $"Alpaca API error: {ex.Message}")
                    };
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Unexpected error: {ex.Message}");
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
                        : Results.Problem($"Alpaca API error: {ex.Message}");
                }
                catch (Exception ex)
                {
                    return Results.Problem($"Unexpected error: {ex.Message}");
                }
            });

            // --- List algos endpoint ---
            app.MapGet("/api/algos", () =>
            {
                var algoFolder = Path.Combine(Directory.GetCurrentDirectory(), "algos-python");

                if (!Directory.Exists(algoFolder))
                    return Results.Ok(new string[] { });

                var algos = Directory.GetFiles(algoFolder, "*.py")
                                    .Select(Path.GetFileNameWithoutExtension)
                                    .ToList();

                return Results.Ok(algos);
            });

            // --- Run Python SMA Algorithm (existing) ---
            app.MapPost("/run-algo", async (IAlpacaDataClient client, AlgoRequest request) =>
            {
                try
                {
                    await LogStream.PushAsync($"[run-algo] Received request for {request.Symbol} short={request.Short} long={request.Long}");


                    var threeMonthsAgo = DateTime.UtcNow.AddMonths(-3);
                    var barsRequest = new HistoricalBarsRequest(request.Symbol, threeMonthsAgo, DateTime.UtcNow, BarTimeFrame.Day)
                    {
                        Feed = MarketDataFeed.Iex
                    };

                    var bars = await client.ListHistoricalBarsAsync(barsRequest);
                    var history = bars.Items.Select(b => new { date = b.TimeUtc.ToString("yyyy-MM-dd"), close = b.Close }).ToList();

                    var inputDict = new Dictionary<string, object>
                    {
                        ["symbol"] = request.Symbol,
                        ["short"] = request.Short,
                        ["long"] = request.Long,
                        ["bars"] = history
                    };

                    var jsonInput = JsonSerializer.Serialize(inputDict);

                    // ✅ Robust Python path resolution
                    var possiblePaths = new[]
                    {
                        Path.Combine(Directory.GetCurrentDirectory(), "algos-python", "sma_signal_runner.py"),
                        Path.Combine("..", "algos-python", "sma_signal_runner.py")
                    };

                    var scriptPath = possiblePaths.FirstOrDefault(File.Exists);
                    if (scriptPath == null)
                    {
                        Console.Error.WriteLine($"[run-algo] ❌ Python script not found. Checked: {string.Join(", ", possiblePaths.Select(Path.GetFullPath))}");
                        return Results.Problem($"Python script not found. Make sure 'algos-python/sma_signal_runner.py' exists.");
                    }

                    await LogStream.PushAsync($"[run-algo] Using Python script: {Path.GetFullPath(scriptPath)}");


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

                    var signal = output
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault()
                        ?.Trim()
                        .ToUpperInvariant() ?? "HOLD";

                    Console.WriteLine($"[run-algo] Python signal for {request.Symbol}: {signal}");
                    await LogStream.PushAsync($"[run-algo] Python signal for {request.Symbol}: {signal}");

                    return Results.Ok(new
                    {
                        symbol = request.Symbol,
                        request.Short,
                        request.Long,
                        signal,
                        bars_used = history.Count
                    });
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[run-algo] Exception: {ex}");
                    if (!string.IsNullOrWhiteSpace(ex.Message))
                    {
                        await LogStream.PushAsync($"[run-algo] Python stderr: {ex.Message}");
                    }
                    return Results.Problem($"Failed to run trading algorithm: {ex.Message}");
                }
            });

            // --- NEW: Run any selected python algo by name (Endpoint A) ---
            app.MapPost("/run-selected-algo", async (IAlpacaDataClient client, RunSelectedAlgoRequest request) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(request.Algo))
                        return Results.Problem("Algo name is required.");

                    // sanitize: allow only letters, numbers, dash, underscore
                    var validName = Regex.IsMatch(request.Algo, @"^[A-Za-z0-9_\-]+$");
                    if (!validName)
                        return Results.Problem("Invalid algo name. Only letters, numbers, dash and underscore are allowed.");

                    await LogStream.PushAsync($"[run-selected-algo] Received request for algo={request.Algo} symbol={request.Symbol} short={request.Short} long={request.Long}");

                    var threeMonthsAgo = DateTime.UtcNow.AddMonths(-3);
                    var barsRequest = new HistoricalBarsRequest(request.Symbol, threeMonthsAgo, DateTime.UtcNow, BarTimeFrame.Day)
                    {
                        Feed = MarketDataFeed.Iex
                    };

                    var bars = await client.ListHistoricalBarsAsync(barsRequest);
                    var history = bars.Items.Select(b => new { date = b.TimeUtc.ToString("yyyy-MM-dd"), close = b.Close }).ToList();

                    var inputDict = new Dictionary<string, object>
                    {
                        ["symbol"] = request.Symbol,
                        ["short"] = request.Short,
                        ["long"] = request.Long,
                        ["bars"] = history
                    };

                    var jsonInput = JsonSerializer.Serialize(inputDict);

                    var algoFolder = Path.Combine(Directory.GetCurrentDirectory(), "algos-python");
                    var possiblePaths = new[]
                    {
                        Path.Combine(algoFolder, $"{request.Algo}.py"),
                        Path.Combine("..", "algos-python", $"{request.Algo}.py")
                    };

                    var scriptPath = possiblePaths.FirstOrDefault(File.Exists);
                    if (scriptPath == null)
                    {
                        Console.Error.WriteLine($"[run-selected-algo] ❌ Python script not found. Checked: {string.Join(", ", possiblePaths.Select(Path.GetFullPath))}");
                        return Results.Problem($"Python script not found. Make sure 'algos-python/{request.Algo}.py' exists.");
                    }

                    await LogStream.PushAsync($"[run-selected-algo] Using Python script: {Path.GetFullPath(scriptPath)}");

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
                        Console.Error.WriteLine("[run-selected-algo] Python stderr:");
                        Console.Error.WriteLine(error);
                    }

                    var signal = output
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault()
                        ?.Trim()
                        .ToUpperInvariant() ?? "HOLD";

                    Console.WriteLine($"[run-selected-algo] Python signal for {request.Symbol}: {signal}");
                    await LogStream.PushAsync($"[run-selected-algo] Python signal for {request.Symbol}: {signal}");

                    return Results.Ok(new
                    {
                        symbol = request.Symbol,
                        request.Short,
                        request.Long,
                        signal,
                        bars_used = history.Count
                    });
                }
                catch (RestClientErrorException ex)
                {
                    return ex.ErrorCode switch
                    {
                        401 => Results.Problem(statusCode: 401, detail: "Authentication failed. Check your API keys."),
                        403 => Results.Problem(statusCode: 403, detail: "Your API keys do not have permission for this request."),
                        422 => Results.NotFound($"Symbol '{request.Symbol}' not found."),
                        429 => Results.Problem(statusCode: 429, detail: "Rate limit exceeded. Please try again later."),
                        _ => Results.Problem(statusCode: 500, detail: $"Alpaca API error: {ex.Message}")
                    };
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[run-selected-algo] Exception: {ex}");
                    if (!string.IsNullOrWhiteSpace(ex.Message))
                    {
                        await LogStream.PushAsync($"[run-selected-algo] Python stderr: {ex.Message}");
                    }
                    return Results.Problem($"Failed to run selected trading algorithm: {ex.Message}");
                }
            });

            // START repeated execution
            app.MapPost("/run-selected-algo/start", async (
                IAlpacaDataClient client,
                RunSelectedAlgoStartRequest req) =>
            {
                if (_isRunning)
                    return Results.Problem("An algo is already running.");

                _runCts = new CancellationTokenSource();
                _isRunning = true;

                _ = Task.Run(async () =>
                {
                    var endTime = DateTime.UtcNow.AddMinutes(req.DurationMinutes);

                    while (!_runCts.IsCancellationRequested && DateTime.UtcNow < endTime)
                    {
                        try
                        {
                            await LogStream.PushAsync($"Running {req.Algo}...");

                            // Reuse existing endpoint logic directly
                            var result = await RunAlgoOnce(client, req);

                            await LogStream.PushAsync($"Signal: {result.Signal}");
                        }
                        catch (Exception ex)
                        {
                            await LogStream.PushAsync($"Algo error: {ex.Message}");
                        }

                        await Task.Delay(TimeSpan.FromSeconds(req.IntervalSeconds), _runCts.Token);
                    }

                    _isRunning = false;
                    await LogStream.PushAsync("Algo execution completed.");
                });

                return Results.Ok(new { status = "started" });
            });

            // STOP repeated execution
            app.MapPost("/run-selected-algo/stop", () =>
            {
                if (!_isRunning)
                    return Results.Ok(new { status = "already_stopped" });

                _runCts.Cancel();
                _isRunning = false;
                return Results.Ok(new { status = "stopped" });
            });


            // --- Execute trade based on Python signal (existing) ---
            app.MapPost("/trade/execute", async (IAlpacaDataClient dataClient, IAlpacaTradingClient tradingClient, TradeRequest request) =>
            {
                try
                {
                    if (request.Quantity <= 0)
                    {
                        return Results.Problem("Quantity must be greater than zero.");
                    }

                    var threeMonthsAgo = DateTime.UtcNow.AddMonths(-3);
                    var barsRequest = new HistoricalBarsRequest(request.Symbol, threeMonthsAgo, DateTime.UtcNow, BarTimeFrame.Day)
                    {
                        Feed = MarketDataFeed.Iex
                    };

                    var bars = await dataClient.ListHistoricalBarsAsync(barsRequest);
                    var history = bars.Items.Select(b => new
                    {
                        date = b.TimeUtc.ToString("yyyy-MM-dd"),
                        close = b.Close
                    }).ToList();

                    if (!history.Any())
                    {
                        return Results.Problem("No historical data retrieved for the requested symbol.");
                    }

                    var inputDict = new Dictionary<string, object>
                    {
                        ["symbol"] = request.Symbol,
                        ["short"] = request.Short,
                        ["long"] = request.Long,
                        ["bars"] = history
                    };

                    var jsonInput = JsonSerializer.Serialize(inputDict);
                    var possiblePaths = new[]
                    {
                        Path.Combine(Directory.GetCurrentDirectory(), "algos-python", "sma_signal_runner.py"),
                        Path.Combine("..", "algos-python", "sma_signal_runner.py")
                    };
                    var scriptPath = possiblePaths.FirstOrDefault(File.Exists);
                    if (scriptPath == null)
                    {
                        Console.Error.WriteLine($"[trade/execute] ❌ Python script not found. Checked: {string.Join(", ", possiblePaths.Select(Path.GetFullPath))}");
                        return Results.Problem($"Python script not found. Make sure 'algos-python/sma_signal_runner.py' exists.");
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

                    var signal = output
                        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .FirstOrDefault()
                        ?.Trim()
                        .ToUpperInvariant() ?? "HOLD";

                    Console.WriteLine($"[trade/execute] Signal for {request.Symbol}: {signal}");

                    if (signal == "HOLD")
                    {
                        return Results.Ok(new { symbol = request.Symbol, signal, message = "No action taken." });
                    }

                    var side = signal == "BUY" ? OrderSide.Buy : OrderSide.Sell;
                    var quantity = OrderQuantity.Fractional(request.Quantity);
                    var orderRequest = new NewOrderRequest(request.Symbol, quantity, side, OrderType.Market, TimeInForce.Day);
                    var order = await tradingClient.PostOrderAsync(orderRequest);

                    return Results.Ok(new
                    {
                        symbol = request.Symbol,
                        signal,
                        orderId = order.OrderId,
                        quantity = request.Quantity,
                        side = side.ToString()
                    });
                }
                catch (RestClientErrorException ex)
                {
                    return ex.ErrorCode switch
                    {
                        401 => Results.Problem(statusCode: 401, detail: "Authentication failed. Check your API keys."),
                        403 => Results.Problem(statusCode: 403, detail: "Your API keys do not have permission for this request."),
                        422 => Results.NotFound("Trading request rejected. Check symbol or account settings."),
                        429 => Results.Problem(statusCode: 429, detail: "Rate limit exceeded. Please try again later."),
                        _ => Results.Problem(statusCode: 500, detail: $"An Alpaca API error occurred: {ex.Message}")
                    };
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[trade/execute] Exception: {ex}");
                    return Results.Problem($"Failed to execute trade: {ex.Message}");
                }
            });

            // --- NEW: Validate ticker endpoint (Endpoint D) ---
            // --- NEW: Validate ticker endpoint (Endpoint D) ---
            app.MapPost("/api/tickers/validate", async (IAlpacaDataClient client, ValidateTickerRequest request) =>
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(request.Symbol))
                        return Results.BadRequest("Symbol is required.");

                    var symbol = request.Symbol.Trim().ToUpperInvariant();

                    // Try to fetch latest quote to confirm the symbol exists
                    var quoteReq = new LatestMarketDataRequest(symbol)
                    {
                        Feed = MarketDataFeed.Iex
                    };

                    try
                    {
                        var quote = await client.GetLatestQuoteAsync(quoteReq);

                        // If API returns null or 0 price, treat it as invalid
                        if (quote is null || (quote.AskPrice <= 0 && quote.BidPrice <= 0))
                        {
                            return Results.NotFound(new
                            {
                                symbol,
                                valid = false,
                                message = "Symbol not recognized or has no valid market data."
                            });
                        }

                        return Results.Ok(new
                        {
                            symbol,
                            valid = true,
                            message = "Valid ticker symbol."
                        });
                    }
                    catch (RestClientErrorException ex)
                    {
                        return ex.ErrorCode switch
                        {
                            422 => Results.NotFound(new
                            {
                                symbol,
                                valid = false,
                                message = "Invalid ticker symbol."
                            }),
                            401 => Results.Problem(statusCode: 401, detail: "Authentication failed."),
                            403 => Results.Problem(statusCode: 403, detail: "Permission denied by Alpaca."),
                            429 => Results.Problem(statusCode: 429, detail: "Rate limit exceeded."),
                            _ => Results.Problem(statusCode: 500, detail: $"Alpaca API error: {ex.Message}")
                        };
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[validate-ticker] Exception: {ex}");
                    return Results.Problem($"Unexpected error validating ticker: {ex.Message}");
                }
            });


            app.MapGet("/logs/stream", async (HttpContext context) =>
            {
                context.Response.Headers["Content-Type"] = "text/event-stream";
                context.Response.Headers["Cache-Control"] = "no-cache";
                context.Response.Headers["Connection"] = "keep-alive";

                var response = context.Response;

                // Add client to stream list
                LogStream.AddClient(response);

                // Send initial message
                await response.WriteAsync($"data: connected\n\n");
                await response.Body.FlushAsync();

                // Keep the connection alive
                try
                {
                    while (!context.RequestAborted.IsCancellationRequested)
                    {
                        await Task.Delay(1000, context.RequestAborted);
                        await response.WriteAsync($"data: ping\n\n");
                        await response.Body.FlushAsync();
                    }
                }
                catch (TaskCanceledException)
                {
                    // Normal disconnect
                }

                // Remove client
                LogStream.RemoveClient(response);
            });



            await app.RunAsync();
        }
    }
}
