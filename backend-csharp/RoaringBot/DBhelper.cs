// Import the necessary package for PostgreSQL connection
using Npgsql;
using System;
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
    }
}