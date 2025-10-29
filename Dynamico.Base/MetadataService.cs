using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Npgsql;

namespace Dynamico.Base;

public class MetadataService
{
    private readonly string _connectionString;
    // This table stores our application's metadata.
    private const string METADATA_TABLE = "app_managed_tables";

    public MetadataService(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Creates the metadata table if it does not exist.
    /// This should be run at application start-up.
    /// </summary>
    public async Task EnsureMetadataTableExistsAsync()
    {
        // Note: This table name is hard-coded and safe.
        string sql = $@"
            CREATE TABLE IF NOT EXISTS {METADATA_TABLE} (
                table_id SERIAL PRIMARY KEY,
                table_name VARCHAR(100) UNIQUE NOT NULL,
                owner_user_id INT NOT NULL,
                created_at TIMESTAMPTZ DEFAULT NOW()
            );";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync(sql);
    }

    /// <summary>
    /// Registers a new table in our metadata.
    /// </summary>
    public async Task RegisterTableAsync(string tableName, int ownerUserId)
    {
        // The table name here has already been validated by SqlSafeBuilder
        string sql = $@"INSERT INTO {METADATA_TABLE} (table_name, owner_user_id) 
                        VALUES (@TableName, @OwnerUserId);";

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync(sql, new { TableName = tableName, OwnerUserId = ownerUserId });
    }

    /// <summary>
    /// Checks if a user is allowed to access a specific table.
    /// </summary>
    /// <param name="tableName">The table to check.</param>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="isSuperAdmin">Flag for super admin (bypasses check).</param>
    /// <returns>True if access is allowed.</returns>
    public async Task<bool> CanUserAccessTableAsync(string tableName, int userId, bool isSuperAdmin)
    {
        // A super admin can access any table, including metadata tables.
        if (isSuperAdmin)
        {
            return true;
        }

        // Prevent clients from ever accessing the metadata table
        if (tableName.Equals(METADATA_TABLE, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string sql = $@"SELECT COUNT(1) 
                        FROM {METADATA_TABLE} 
                        WHERE table_name = @TableName AND owner_user_id = @UserId;";

        await using var connection = new NpgsqlConnection(_connectionString);
        int count = await connection.ExecuteScalarAsync<int>(sql, new { TableName = tableName, UserId = userId });

        return count > 0;
    }
}