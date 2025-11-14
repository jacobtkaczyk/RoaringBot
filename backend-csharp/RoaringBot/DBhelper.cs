// Import the necessary package for PostgreSQL connection
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RoaringBot
{
    /// <summary>
    /// Represents the User model, mirroring the 'users' table in the database schema.
    /// Each property corresponds to a column in the table.
    /// </summary>
    public class User
    {
        public int Id { get; set; } // Primary Key
        public required string Username { get; set; }
        public required string Email { get; set; }
        public required string PasswordHash { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Default to current time
        public DateTime? LastLogin { get; set; } // Nullable for users who haven't logged in
        public decimal AccountBalance { get; set; } = 0; // Default to 0
        public string? ApiKeyHash { get; set; } // Nullable
        public string RiskTolerance { get; set; } = "MEDIUM"; // Default value
        public bool IsActive { get; set; } = true; // Default to active
        public string Timezone { get; set; } = "UTC"; // Default timezone
    }

    /// <summary>
    /// Represents the Trade model, mirroring the 'trades' table in the database schema.
    /// </summary>
    public class Trade
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public required string Symbol { get; set; }
        public required string Action { get; set; } // BUY or SELL
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public DateTime? Timestamp { get; set; }
        public string? StrategyName { get; set; }
        public Guid? OrderId { get; set; }
        public decimal Fees { get; set; } = 0;
        public string Status { get; set; } = "PENDING"; // PENDING, FILLED, CANCELLED
        public DateTime? ExecutionTime { get; set; }
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Represents the Watchlist model, mirroring the 'watchlists' table in the database schema.
    /// </summary>
    public class Watchlist
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public required string Symbol { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents the MarketData model, mirroring the 'market_data' table in the database schema.
    /// </summary>
    public class MarketData
    {
        public int Id { get; set; }
        public required string Symbol { get; set; }
        public DateTime Timestamp { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
    }

    /// <summary>
    /// Represents the AuditLog model, mirroring the 'audit_logs' table in the database schema.
    /// </summary>
    public class AuditLog
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public required string Action { get; set; }
        public string? Details { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? IpAddress { get; set; }
    }

    /// <summary>
    /// A helper class to manage all database interactions.
    /// It handles creating connections and executing commands.
    /// </summary>
    public class DataBaseHelper
    {
        // Private field to store the database connection string
        private readonly string _connectionString;

        /// <summary>
        /// Constructor for the DataBaseHelper.
        /// It requires a connection string to be provided when an instance is created.
        /// </summary>
        /// <param name="connectionString">The connection string for the PostgreSQL database.</param>
        public DataBaseHelper(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Creates and returns a new NpgsqlConnection object.
        /// This method centralizes the connection creation logic.
        /// </summary>
        /// <returns>A new NpgsqlConnection instance.</returns>
        public NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }

        /// <summary>
        /// Asynchronously saves a new user to the 'users' table in the database.
        /// </summary>
        /// <param name="user">The User object containing the data to be saved.</param>
        /// <returns>The integer ID of the newly inserted user.</returns>
        public async Task<int> SaveUserAsync(User user)
        {
            // 'await using' ensures the connection is properly closed and disposed of, even if errors occur.
            await using var conn = GetConnection();
            await conn.OpenAsync(); // Open the connection to the database

            // Define the SQL INSERT statement with placeholders (@) to prevent SQL injection.
            // RETURNING id; is a PostgreSQL feature to get the ID of the newly inserted row.
            var sql = @"
                INSERT INTO users (
                    username, email, password_hash, created_at, last_login, 
                    account_balance, api_key_hash, risk_tolerance, is_active, timezone
                ) 
                VALUES (
                    @username, @email, @password_hash, @created_at, @last_login, 
                    @account_balance, @api_key_hash, @risk_tolerance, @is_active, @timezone
                )
                RETURNING id;";

            // Create a new command object with the SQL and the connection.
            await using var cmd = new NpgsqlCommand(sql, conn);
            
            // Add parameters to the command. This is the secure way to pass values to a query.
            // It prevents SQL injection attacks by treating user input as literal values, not executable code.
            cmd.Parameters.AddWithValue("username", user.Username);
            cmd.Parameters.AddWithValue("email", user.Email);
            cmd.Parameters.AddWithValue("password_hash", user.PasswordHash);
            cmd.Parameters.AddWithValue("created_at", user.CreatedAt);
            // Handle nullable properties: if null, pass DBNull.Value to the database.
            cmd.Parameters.AddWithValue("last_login", user.LastLogin ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("account_balance", user.AccountBalance);
            cmd.Parameters.AddWithValue("api_key_hash", user.ApiKeyHash ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("risk_tolerance", user.RiskTolerance);
            cmd.Parameters.AddWithValue("is_active", user.IsActive);
            cmd.Parameters.AddWithValue("timezone", user.Timezone);

            // Execute the command and retrieve the single value returned (the new user's ID).
            var newUserId = await cmd.ExecuteScalarAsync();
            
            // Check if the ID was actually returned.
            if (newUserId != null)
            {
                return Convert.ToInt32(newUserId);
            }

            // If no ID was returned, something went wrong.
            throw new Exception("Failed to save user and retrieve ID.");
        }

        /// <summary>
        /// Gets a user by their ID.
        /// </summary>
        public async Task<User?> GetUserByIdAsync(int userId)
        {
            await using var conn = GetConnection();
            await conn.OpenAsync();

            var sql = @"
                SELECT id, username, email, password_hash, created_at, last_login, 
                       account_balance, api_key_hash, risk_tolerance, is_active, timezone
                FROM users
                WHERE id = @id;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", userId);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new User
                {
                    Id = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    Email = reader.GetString(2),
                    PasswordHash = reader.GetString(3),
                    CreatedAt = reader.GetDateTime(4),
                    LastLogin = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    AccountBalance = reader.GetDecimal(6),
                    ApiKeyHash = reader.IsDBNull(7) ? null : reader.GetString(7),
                    RiskTolerance = reader.GetString(8),
                    IsActive = reader.GetBoolean(9),
                    Timezone = reader.GetString(10)
                };
            }

            return null;
        }

        /// <summary>
        /// Gets a user by their username.
        /// </summary>
        public async Task<User?> GetUserByUsernameAsync(string username)
        {
            await using var conn = GetConnection();
            await conn.OpenAsync();

            var sql = @"
                SELECT id, username, email, password_hash, created_at, last_login, 
                       account_balance, api_key_hash, risk_tolerance, is_active, timezone
                FROM users
                WHERE username = @username;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("username", username);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new User
                {
                    Id = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    Email = reader.GetString(2),
                    PasswordHash = reader.GetString(3),
                    CreatedAt = reader.GetDateTime(4),
                    LastLogin = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
                    AccountBalance = reader.GetDecimal(6),
                    ApiKeyHash = reader.IsDBNull(7) ? null : reader.GetString(7),
                    RiskTolerance = reader.GetString(8),
                    IsActive = reader.GetBoolean(9),
                    Timezone = reader.GetString(10)
                };
            }

            return null;
        }

        /// <summary>
        /// Saves a trade to the database.
        /// </summary>
        public async Task<int> SaveTradeAsync(Trade trade)
        {
            await using var conn = GetConnection();
            await conn.OpenAsync();

            var sql = @"
                INSERT INTO trades (
                    user_id, symbol, action, quantity, price, timestamp, 
                    strategy_name, order_id, fees, status, execution_time, notes
                )
                VALUES (
                    @user_id, @symbol, @action, @quantity, @price, @timestamp,
                    @strategy_name, @order_id, @fees, @status, @execution_time, @notes
                )
                RETURNING id;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("user_id", trade.UserId);
            cmd.Parameters.AddWithValue("symbol", trade.Symbol);
            cmd.Parameters.AddWithValue("action", trade.Action);
            cmd.Parameters.AddWithValue("quantity", trade.Quantity);
            cmd.Parameters.AddWithValue("price", trade.Price);
            cmd.Parameters.AddWithValue("timestamp", trade.Timestamp ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("strategy_name", trade.StrategyName ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("order_id", trade.OrderId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("fees", trade.Fees);
            cmd.Parameters.AddWithValue("status", trade.Status);
            cmd.Parameters.AddWithValue("execution_time", trade.ExecutionTime ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("notes", trade.Notes ?? (object)DBNull.Value);

            var result = await cmd.ExecuteScalarAsync();
            if (result != null)
            {
                return Convert.ToInt32(result);
            }

            throw new Exception("Failed to save trade and retrieve ID.");
        }

        /// <summary>
        /// Gets all trades for a specific user, ordered by timestamp descending.
        /// </summary>
        public async Task<List<Trade>> GetTradesByUserIdAsync(int userId, int limit = 100)
        {
            await using var conn = GetConnection();
            await conn.OpenAsync();

            var sql = @"
                SELECT id, user_id, symbol, action, quantity, price, timestamp,
                       strategy_name, order_id, fees, status, execution_time, notes
                FROM trades
                WHERE user_id = @user_id
                ORDER BY timestamp DESC NULLS LAST, id DESC
                LIMIT @limit;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("user_id", userId);
            cmd.Parameters.AddWithValue("limit", limit);

            var trades = new List<Trade>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                trades.Add(new Trade
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Symbol = reader.GetString(2),
                    Action = reader.GetString(3),
                    Quantity = reader.GetDecimal(4),
                    Price = reader.GetDecimal(5),
                    Timestamp = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                    StrategyName = reader.IsDBNull(7) ? null : reader.GetString(7),
                    OrderId = reader.IsDBNull(8) ? null : reader.GetGuid(8),
                    Fees = reader.GetDecimal(9),
                    Status = reader.GetString(10),
                    ExecutionTime = reader.IsDBNull(11) ? null : reader.GetDateTime(11),
                    Notes = reader.IsDBNull(12) ? null : reader.GetString(12)
                });
            }

            return trades;
        }

        /// <summary>
        /// Adds a symbol to a user's watchlist. Returns the ID if successful, or 0 if already exists.
        /// </summary>
        public async Task<int> AddToWatchlistAsync(Watchlist watchlist)
        {
            await using var conn = GetConnection();
            await conn.OpenAsync();

            // Use INSERT ... ON CONFLICT to handle duplicates gracefully
            var sql = @"
                INSERT INTO watchlists (user_id, symbol, added_at)
                VALUES (@user_id, @symbol, @added_at)
                ON CONFLICT (user_id, symbol) DO NOTHING
                RETURNING id;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("user_id", watchlist.UserId);
            cmd.Parameters.AddWithValue("symbol", watchlist.Symbol);
            cmd.Parameters.AddWithValue("added_at", watchlist.AddedAt);

            var result = await cmd.ExecuteScalarAsync();
            return result != null ? Convert.ToInt32(result) : 0;
        }

        /// <summary>
        /// Gets all symbols in a user's watchlist.
        /// </summary>
        public async Task<List<Watchlist>> GetWatchlistByUserIdAsync(int userId)
        {
            await using var conn = GetConnection();
            await conn.OpenAsync();

            var sql = @"
                SELECT id, user_id, symbol, added_at
                FROM watchlists
                WHERE user_id = @user_id
                ORDER BY added_at DESC;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("user_id", userId);

            var watchlist = new List<Watchlist>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                watchlist.Add(new Watchlist
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Symbol = reader.GetString(2),
                    AddedAt = reader.GetDateTime(3)
                });
            }

            return watchlist;
        }

        /// <summary>
        /// Removes a symbol from a user's watchlist.
        /// </summary>
        public async Task<bool> RemoveFromWatchlistAsync(int userId, string symbol)
        {
            await using var conn = GetConnection();
            await conn.OpenAsync();

            var sql = @"
                DELETE FROM watchlists
                WHERE user_id = @user_id AND symbol = @symbol;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("user_id", userId);
            cmd.Parameters.AddWithValue("symbol", symbol);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }

        /// <summary>
        /// Saves market data. Uses UPSERT (INSERT ... ON CONFLICT) to update existing records.
        /// </summary>
        public async Task<int> SaveMarketDataAsync(MarketData marketData)
        {
            await using var conn = GetConnection();
            await conn.OpenAsync();

            var sql = @"
                INSERT INTO market_data (symbol, timestamp, open, high, low, close, volume)
                VALUES (@symbol, @timestamp, @open, @high, @low, @close, @volume)
                ON CONFLICT (symbol, timestamp) 
                DO UPDATE SET 
                    open = EXCLUDED.open,
                    high = EXCLUDED.high,
                    low = EXCLUDED.low,
                    close = EXCLUDED.close,
                    volume = EXCLUDED.volume
                RETURNING id;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("symbol", marketData.Symbol);
            cmd.Parameters.AddWithValue("timestamp", marketData.Timestamp);
            cmd.Parameters.AddWithValue("open", marketData.Open);
            cmd.Parameters.AddWithValue("high", marketData.High);
            cmd.Parameters.AddWithValue("low", marketData.Low);
            cmd.Parameters.AddWithValue("close", marketData.Close);
            cmd.Parameters.AddWithValue("volume", marketData.Volume);

            var result = await cmd.ExecuteScalarAsync();
            if (result != null)
            {
                return Convert.ToInt32(result);
            }

            throw new Exception("Failed to save market data and retrieve ID.");
        }

        /// <summary>
        /// Creates an audit log entry.
        /// </summary>
        public async Task<int> LogAuditAsync(AuditLog auditLog)
        {
            await using var conn = GetConnection();
            await conn.OpenAsync();

            var sql = @"
                INSERT INTO audit_logs (user_id, action, details, timestamp, ip_address)
                VALUES (@user_id, @action, @details, @timestamp, @ip_address)
                RETURNING id;";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("user_id", auditLog.UserId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("action", auditLog.Action);
            cmd.Parameters.AddWithValue("details", auditLog.Details ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("timestamp", auditLog.Timestamp);
            cmd.Parameters.AddWithValue("ip_address", auditLog.IpAddress ?? (object)DBNull.Value);

            var result = await cmd.ExecuteScalarAsync();
            if (result != null)
            {
                return Convert.ToInt32(result);
            }

            throw new Exception("Failed to create audit log and retrieve ID.");
        }
    }
}