using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using Npgsql;

namespace Dynamico.Base;

public record ColumnDefinition(string Name, string DataType);

public class DynamicDbService
{
    private readonly string _connectionString;
    private readonly MetadataService _metadataService;

    // Simulate user roles. In a real app, this would come from auth.
    private const int SUPER_ADMIN_USER_ID = 1;
    private bool IsSuperAdmin(int userId) => userId == SUPER_ADMIN_USER_ID;


    public DynamicDbService(string connectionString)
    {
        _connectionString = connectionString;
        _metadataService = new MetadataService(connectionString);
    }

    /// <summary>
    /// Creates a new table dynamically and registers it.
    /// </summary>
    public async Task CreateTableAsync(string tableName, List<ColumnDefinition> columns, int currentUserId)
    {
        // 1. --- SECURITY VALIDATION ---
        // This is the most important step for DDL.
        string safeTableName = SqlSafeBuilder.QuoteIdentifier(tableName);
        var safeColumns = new List<string>();

        foreach (var col in columns)
        {
            string safeColName = SqlSafeBuilder.QuoteIdentifier(col.Name);
            string safeColType = SqlSafeBuilder.ValidateDataType(col.DataType); // e.g., "INT", "VARCHAR(100)"
            safeColumns.Add($"{safeColName} {safeColType}");
        }

        // 2. --- BUILD DDL QUERY ---
        var ddlBuilder = new StringBuilder();
        ddlBuilder.Append($"CREATE TABLE {safeTableName} (");
        ddlBuilder.Append("id SERIAL PRIMARY KEY, "); // Add a default PK
        ddlBuilder.Append(string.Join(", ", safeColumns));
        ddlBuilder.Append(");");

        // 3. --- EXECUTE DDL ---
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync(ddlBuilder.ToString());

        // 4. --- REGISTER IN METADATA ---
        // Register the "unquoted" name for easier look-up
        await _metadataService.RegisterTableAsync(tableName, currentUserId);

        Console.WriteLine($"Table '{tableName}' created successfully.");
    }

    /// <summary>
    /// Inserts data into a dynamically-created table.
    /// </summary>
    public async Task InsertDataAsync(string tableName, Dictionary<string, object> data, int currentUserId)
    {
        // 1. --- PERMISSION CHECK ---
        bool canAccess = await _metadataService.CanUserAccessTableAsync(
            tableName, currentUserId, IsSuperAdmin(currentUserId));

        if (!canAccess)
        {
            throw new InvalidOperationException($"Access denied to table '{tableName}'.");
        }

        // 2. --- SECURITY VALIDATION (Schema) ---
        string safeTableName = SqlSafeBuilder.QuoteIdentifier(tableName);

        var safeColumnNames = data.Keys
            .Select(SqlSafeBuilder.QuoteIdentifier)
            .ToList();

        // 3. --- SECURITY PARAMETERIZATION (Values) ---
        // Dapper handles this part. We create parameter names.
        var parameterNames = data.Keys
            .Select(key => $"@{key}")
            .ToList();

        // 4. --- BUILD DML QUERY ---
        string sql = $"INSERT INTO {safeTableName} ({string.Join(", ", safeColumnNames)}) " +
                     $"VALUES ({string.Join(", ", parameterNames)});";

        // 5. --- EXECUTE DML (with Dapper parameters) ---
        // We create a Dapper DynamicParameters object.
        var parameters = new DynamicParameters();
        foreach (var entry in data)
        {
            // Dapper will map 'Name' to '@Name'
            parameters.Add(entry.Key, entry.Value);
        }

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.ExecuteAsync(sql, parameters);
        Console.WriteLine($"Data inserted into '{tableName}'.");
    }

    /// <summary>
    /// Queries data from a dynamically-created table.
    /// </summary>
    public async Task<IEnumerable<dynamic>> QueryDataAsync(string tableName, int currentUserId)
    {
        // 1. --- PERMISSION CHECK ---
        bool canAccess = await _metadataService.CanUserAccessTableAsync(
            tableName, currentUserId, IsSuperAdmin(currentUserId));

        if (!canAccess)
        {
            throw new InvalidOperationException($"Access denied to table '{tableName}'.");
        }
        // 2. --- SECURITY VALIDATION (Schema) ---
        string safeTableName = SqlSafeBuilder.QuoteIdentifier(tableName);

        // 3. --- BUILD DML QUERY ---
        // For a real app, you would add WHERE, LIMIT, etc.
        // The WHERE clause values MUST be parameterized.
        string sql = $"SELECT * FROM {safeTableName} LIMIT 10;";

        await using var connection = new NpgsqlConnection(_connectionString);

        // Dapper's 'dynamic' return type is perfect for this
        return await connection.QueryAsync(sql);
    }
}