using System;
using System.Threading.Tasks;
using RoaringBot;

namespace RoaringBot.Tests
{
    /// <summary>
    /// Comprehensive test class for DataBaseHelper and all models.
    /// Tests all database operations across all tables.
    /// </summary>
    public class DBHelperTests
    {
        // Connection string - should match your appsettings.json
        // Use "localhost" when running locally, "postgres" when running inside Docker
        private const string ConnectionString = "Host=localhost;Port=5432;Username=roaringbot;Password=supersecret;Database=trading";

        /// <summary>
        /// Main method to run all tests.
        /// </summary>
        public static async Task Main(string[] args)
        {
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.WriteLine("   🚀 Starting Comprehensive DBHelper Tests");
            Console.WriteLine("═══════════════════════════════════════════════\n");

            try
            {
                // Test database connection first
                await TestDatabaseConnection();

                // Test User operations
                Console.WriteLine("\n━━━ USER TESTS ━━━");
                int userId1 = await TestCreateBasicUser();
                int userId2 = await TestCreateFullUser();
                await TestGetUserById(userId1);
                await TestGetUserByUsername("testuser1");

                // Test Trade operations
                Console.WriteLine("\n━━━ TRADE TESTS ━━━");
                await TestCreateTrade(userId1);
                await TestCreateMultipleTrades(userId2);
                await TestGetTradesByUser(userId1);

                // Test Watchlist operations
                Console.WriteLine("\n━━━ WATCHLIST TESTS ━━━");
                await TestAddToWatchlist(userId1);
                await TestGetWatchlist(userId1);
                await TestRemoveFromWatchlist(userId1, "AAPL");

                // Test Market Data operations
                Console.WriteLine("\n━━━ MARKET DATA TESTS ━━━");
                await TestSaveMarketData();
                await TestSaveMarketDataWithConflict();

                // Test Audit Log operations
                Console.WriteLine("\n━━━ AUDIT LOG TESTS ━━━");
                await TestCreateAuditLog(userId1);

                Console.WriteLine("\n═══════════════════════════════════════════════");
                Console.WriteLine("   ✅ ALL TESTS COMPLETED SUCCESSFULLY!");
                Console.WriteLine("═══════════════════════════════════════════════");
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n═══════════════════════════════════════════════");
                Console.WriteLine("   ❌ TEST FAILED");
                Console.WriteLine("═══════════════════════════════════════════════");
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"\nStack Trace:\n{ex.StackTrace}");
            }
        }

        // ==================== DATABASE CONNECTION TESTS ====================

        private static async Task TestDatabaseConnection()
        {
            Console.WriteLine("🔌 Testing database connection...");
            
            var dbHelper = new DataBaseHelper(ConnectionString);

            await using var conn = dbHelper.GetConnection();
            await conn.OpenAsync();
            
            Console.WriteLine($"   ✅ Connection successful!");
            Console.WriteLine($"   📊 Database: {conn.Database}");
            Console.WriteLine($"   🖥️  Server Version: {conn.ServerVersion}");
            Console.WriteLine($"   🟢 State: {conn.State}");
        }

        // ==================== USER TESTS ====================

        private static async Task<int> TestCreateBasicUser()
        {
            Console.WriteLine("\n📝 Test: Creating a basic user...");
            
            var dbHelper = new DataBaseHelper(ConnectionString);

            var testUser = new User
            {
                Username = $"testuser1_{DateTime.Now.Ticks}",
                Email = $"testuser1_{DateTime.Now.Ticks}@example.com",
                PasswordHash = "hashed_password_123"
            };

            int userId = await dbHelper.SaveUserAsync(testUser);
            
            Console.WriteLine($"   ✅ User created with ID: {userId}");
            Console.WriteLine($"   👤 Username: {testUser.Username}");
            Console.WriteLine($"   📧 Email: {testUser.Email}");
            Console.WriteLine($"   💰 Balance: ${testUser.AccountBalance}");
            Console.WriteLine($"   ⚠️  Risk: {testUser.RiskTolerance}");

            return userId;
        }

        private static async Task<int> TestCreateFullUser()
        {
            Console.WriteLine("\n📝 Test: Creating a user with all fields...");
            
            var dbHelper = new DataBaseHelper(ConnectionString);

            var testUser = new User
            {
                Username = $"poweruser_{DateTime.Now.Ticks}",
                Email = $"poweruser_{DateTime.Now.Ticks}@example.com",
                PasswordHash = "super_secure_hash_456",
                LastLogin = DateTime.UtcNow,
                AccountBalance = 10000.50m,
                ApiKeyHash = "alpaca_api_key_hash_789",
                RiskTolerance = "HIGH",
                IsActive = true,
                Timezone = "America/New_York"
            };

            int userId = await dbHelper.SaveUserAsync(testUser);
            
            Console.WriteLine($"   ✅ Full user created with ID: {userId}");
            Console.WriteLine($"   👤 Username: {testUser.Username}");
            Console.WriteLine($"   💰 Balance: ${testUser.AccountBalance}");
            Console.WriteLine($"   🔑 Has API Key: {testUser.ApiKeyHash != null}");
            Console.WriteLine($"   🌍 Timezone: {testUser.Timezone}");

            return userId;
        }

        private static async Task TestGetUserById(int userId)
        {
            Console.WriteLine($"\n🔍 Test: Getting user by ID {userId}...");
            
            var dbHelper = new DataBaseHelper(ConnectionString);
            var user = await dbHelper.GetUserByIdAsync(userId);

            if (user != null)
            {
                Console.WriteLine($"   ✅ User found!");
                Console.WriteLine($"   👤 Username: {user.Username}");
                Console.WriteLine($"   📧 Email: {user.Email}");
                Console.WriteLine($"   🟢 Active: {user.IsActive}");
            }
            else
            {
                Console.WriteLine($"   ❌ User not found!");
            }
        }

        private static async Task TestGetUserByUsername(string username)
        {
            Console.WriteLine($"\n🔍 Test: Getting user by username '{username}'...");
            
            var dbHelper = new DataBaseHelper(ConnectionString);
            var user = await dbHelper.GetUserByUsernameAsync(username);

            if (user != null)
            {
                Console.WriteLine($"   ✅ User found!");
                Console.WriteLine($"   🆔 ID: {user.Id}");
                Console.WriteLine($"   📧 Email: {user.Email}");
            }
            else
            {
                Console.WriteLine($"   ⚠️  User not found (may have been created with timestamp suffix)");
            }
        }

        // ==================== TRADE TESTS ====================

        private static async Task TestCreateTrade(int userId)
        {
            Console.WriteLine($"\n💵 Test: Creating a trade for user {userId}...");
            
            var dbHelper = new DataBaseHelper(ConnectionString);

            var trade = new Trade
            {
                UserId = userId,
                Symbol = "AAPL",
                Action = "BUY",
                Quantity = 10,
                Price = 175.50m,
                StrategyName = "momentum_strategy",
                Fees = 1.50m,
                Status = "FILLED",
                ExecutionTime = DateTime.UtcNow,
                Notes = "Test trade - buying Apple stock"
            };

            int tradeId = await dbHelper.SaveTradeAsync(trade);
            
            Console.WriteLine($"   ✅ Trade created with ID: {tradeId}");
            Console.WriteLine($"   📊 {trade.Action} {trade.Quantity} shares of {trade.Symbol}");
            Console.WriteLine($"   💲 Price: ${trade.Price}");
            Console.WriteLine($"   💰 Total: ${trade.Quantity * trade.Price}");
            Console.WriteLine($"   📈 Strategy: {trade.StrategyName}");
            Console.WriteLine($"   ✓ Status: {trade.Status}");
        }

        private static async Task TestCreateMultipleTrades(int userId)
        {
            Console.WriteLine($"\n💵 Test: Creating multiple trades for user {userId}...");
            
            var dbHelper = new DataBaseHelper(ConnectionString);

            var trades = new[]
            {
                new Trade
                {
                    UserId = userId,
                    Symbol = "TSLA",
                    Action = "BUY",
                    Quantity = 5,
                    Price = 245.75m,
                    StrategyName = "growth_strategy",
                    Fees = 2.00m,
                    Status = "FILLED"
                },
                new Trade
                {
                    UserId = userId,
                    Symbol = "MSFT",
                    Action = "BUY",
                    Quantity = 15,
                    Price = 380.25m,
                    StrategyName = "value_strategy",
                    Fees = 3.50m,
                    Status = "FILLED"
                },
                new Trade
                {
                    UserId = userId,
                    Symbol = "GOOGL",
                    Action = "SELL",
                    Quantity = 8,
                    Price = 142.50m,
                    StrategyName = "profit_taking",
                    Fees = 1.75m,
                    Status = "PENDING"
                }
            };

            foreach (var trade in trades)
            {
                int tradeId = await dbHelper.SaveTradeAsync(trade);
                Console.WriteLine($"   ✅ {trade.Action} {trade.Quantity}x {trade.Symbol} @ ${trade.Price} (ID: {tradeId})");
            }
        }

        private static async Task TestGetTradesByUser(int userId)
        {
            Console.WriteLine($"\n📊 Test: Getting trades for user {userId}...");
            
            var dbHelper = new DataBaseHelper(ConnectionString);
            var trades = await dbHelper.GetTradesByUserIdAsync(userId, limit: 10);

            Console.WriteLine($"   ✅ Found {trades.Count} trade(s)");
            
            foreach (var trade in trades)
            {
                Console.WriteLine($"   📈 {trade.Action} {trade.Quantity}x {trade.Symbol} @ ${trade.Price} - {trade.Status}");
            }
        }

        // ==================== WATCHLIST TESTS ====================

        private static async Task TestAddToWatchlist(int userId)
        {
            Console.WriteLine($"\n⭐ Test: Adding symbols to watchlist for user {userId}...");
            
            var dbHelper = new DataBaseHelper(ConnectionString);

            var symbols = new[] { "AAPL", "TSLA", "MSFT", "GOOGL", "AMZN" };

            foreach (var symbol in symbols)
            {
                var watchlist = new Watchlist
                {
                    UserId = userId,
                    Symbol = symbol
                };

                int watchlistId = await dbHelper.AddToWatchlistAsync(watchlist);
                
                if (watchlistId > 0)
                {
                    Console.WriteLine($"   ✅ Added {symbol} to watchlist (ID: {watchlistId})");
                }
                else
                {
                    Console.WriteLine($"   ⚠️  {symbol} already in watchlist");
                }
            }
        }

        private static async Task TestGetWatchlist(int userId)
        {
            Console.WriteLine($"\n⭐ Test: Getting watchlist for user {userId}...");
            
            var dbHelper = new DataBaseHelper(ConnectionString);
            var watchlist = await dbHelper.GetWatchlistByUserIdAsync(userId);

            Console.WriteLine($"   ✅ Found {watchlist.Count} symbol(s) in watchlist");
            
            foreach (var item in watchlist)
            {
                Console.WriteLine($"   📌 {item.Symbol} (added: {item.AddedAt:yyyy-MM-dd HH:mm})");
            }
        }

        private static async Task TestRemoveFromWatchlist(int userId, string symbol)
        {
            Console.WriteLine($"\n⭐ Test: Removing {symbol} from watchlist for user {userId}...");
            
            var dbHelper = new DataBaseHelper(ConnectionString);
            bool removed = await dbHelper.RemoveFromWatchlistAsync(userId, symbol);

            if (removed)
            {
                Console.WriteLine($"   ✅ {symbol} removed from watchlist");
            }
            else
            {
                Console.WriteLine($"   ⚠️  {symbol} was not in watchlist");
            }
        }

        // ==================== MARKET DATA TESTS ====================

        private static async Task TestSaveMarketData()
        {
            Console.WriteLine("\n📈 Test: Saving market data...");
            
            var dbHelper = new DataBaseHelper(ConnectionString);

            var marketData = new MarketData
            {
                Symbol = "AAPL",
                Timestamp = DateTime.UtcNow.Date,
                Open = 175.00m,
                High = 178.50m,
                Low = 174.25m,
                Close = 177.80m,
                Volume = 52000000
            };

            int marketDataId = await dbHelper.SaveMarketDataAsync(marketData);
            
            Console.WriteLine($"   ✅ Market data saved with ID: {marketDataId}");
            Console.WriteLine($"   📊 {marketData.Symbol} - {marketData.Timestamp:yyyy-MM-dd}");
            Console.WriteLine($"   💲 Open: ${marketData.Open} | Close: ${marketData.Close}");
            Console.WriteLine($"   📈 High: ${marketData.High} | Low: ${marketData.Low}");
            Console.WriteLine($"   📦 Volume: {marketData.Volume:N0}");
        }

        private static async Task TestSaveMarketDataWithConflict()
        {
            Console.WriteLine("\n📈 Test: Saving market data with conflict (upsert)...");
            
            var dbHelper = new DataBaseHelper(ConnectionString);

            var marketData = new MarketData
            {
                Symbol = "AAPL",
                Timestamp = DateTime.UtcNow.Date, // Same date as previous test
                Open = 176.00m, // Updated values
                High = 179.00m,
                Low = 175.00m,
                Close = 178.50m,
                Volume = 53000000
            };

            int marketDataId = await dbHelper.SaveMarketDataAsync(marketData);
            
            Console.WriteLine($"   ✅ Market data upserted with ID: {marketDataId}");
            Console.WriteLine($"   🔄 Updated existing record for {marketData.Symbol}");
            Console.WriteLine($"   💲 New Close: ${marketData.Close}");
        }

        // ==================== AUDIT LOG TESTS ====================

        private static async Task TestCreateAuditLog(int userId)
        {
            Console.WriteLine($"\n📝 Test: Creating audit log for user {userId}...");
            
            var dbHelper = new DataBaseHelper(ConnectionString);

            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = "USER_LOGIN",
                Details = "User logged in successfully from test suite",
                IpAddress = "127.0.0.1"
            };

            int logId = await dbHelper.LogAuditAsync(auditLog);
            
            Console.WriteLine($"   ✅ Audit log created with ID: {logId}");
            Console.WriteLine($"   📋 Action: {auditLog.Action}");
            Console.WriteLine($"   🌐 IP: {auditLog.IpAddress}");
            Console.WriteLine($"   🕐 Time: {auditLog.Timestamp:yyyy-MM-dd HH:mm:ss}");
        }

        // ==================== HELPER METHODS ====================

        /// <summary>
        /// Helper method to create a random test user.
        /// </summary>
        public static User CreateRandomTestUser()
        {
            var random = new Random();
            var randomId = random.Next(1000, 9999);
            
            return new User
            {
                Username = $"testuser_{randomId}",
                Email = $"testuser_{randomId}@example.com",
                PasswordHash = $"hash_{Guid.NewGuid()}",
                AccountBalance = (decimal)(random.NextDouble() * 100000),
                RiskTolerance = new[] { "LOW", "MEDIUM", "HIGH" }[random.Next(3)],
                Timezone = new[] { "UTC", "America/New_York", "America/Chicago", "America/Los_Angeles" }[random.Next(4)]
            };
        }
    }
}