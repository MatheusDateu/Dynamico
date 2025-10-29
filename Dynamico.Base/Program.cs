using Dapper;
using Dynamico.Base;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class Program
{
    // --- Configuration ---
    const string connectionString = "Host=localhost;Username=postgres;Password=root;Database=dynamico_db";

    // --- Simulate the current user ---
    // ID 1 is Super Admin (can see all).
    // Any other ID (e.g., 2) is a client (can only see their own tables).
    const int currentUserId = 2;

    // --- Dynamic Menu Definition ---

    // Here we map the menu key (e.g., "1") to a tuple containing:
    // 1. A human-readable description.
    // 2. A delegate (Func) pointing to the method to execute.
    private static readonly Dictionary<string, (string Description, Func<DynamicDbService, Task> Action)> _menuActions =
        new()
        {
            { "1", ("Create a new table", HandleCreateTableAsync) },
            { "2", ("Insert data into a table", HandleInsertDataAsync) },
            { "3", ("Query data from a table", HandleQueryDataAsync) },
            { "Q", ("Quit", HandleQuitAsync) }
            // To add a new option (e.g., "4. Drop Table"),
            // you just add one new line here and create the new Handle method.
            // The ShowMenu method does not need to be changed.
        };

    public static async Task Main(string[] args)
    {
        Console.WriteLine("--- Dynamic Database Application ---");
        Console.WriteLine($"--- Logged in as User ID: {currentUserId} ---");

        // Initialise services
        var dbService = new DynamicDbService(connectionString);
        var metadataService = new MetadataService(connectionString);

        // On start-up, ensure the metadata table exists
        await metadataService.EnsureMetadataTableExistsAsync();


        bool isRunning = true;
        while (isRunning)
        {
            // The menu loop is now cleaner and drives execution
            await ShowMenuAsync(dbService);
        }
    }

    /// <summary>
    /// Displays the menu and handles user input by dispatching
    /// to the correct action from the _menuActions dictionary.
    /// </summary>
    private static async Task ShowMenuAsync(DynamicDbService dbService)
    {
        Console.WriteLine("\n--- Main Menu ---");

        // Dynamically display menu options from the dictionary
        foreach (var option in _menuActions)
        {
            Console.WriteLine($"{option.Key}. {option.Value.Description}");
        }

        Console.Write("Select an option: ");
        string choice = Console.ReadLine()?.ToUpper() ?? "";

        try
        {
            // --- NO SWITCH-CASE ---
            // We try to find the choice in the dictionary.
            if (_menuActions.TryGetValue(choice, out var menuAction))
            {
                // If found, we execute the associated Action delegate
                // and pass the dbService instance to it.
                await menuAction.Action(dbService);
            }
            else
            {
                Console.WriteLine("Invalid option. Please try again.");
            }
        }
        catch (Exception ex)
        {
            // Catch errors from the services (e.g., validation, permissions)
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\nERROR: {ex.Message}");
            Console.ResetColor();
        }
    }

    // --- Menu Action Handlers ---

    /// <summary>
    /// Handles the "Create Table" action dynamically.
    /// </summary>
    private static async Task HandleCreateTableAsync(DynamicDbService dbService)
    {
        Console.Write("Enter new table name (e.g., 'contacts'): ");
        string tableName = Console.ReadLine() ?? "";

        var columns = new List<ColumnDefinition>();
        Console.WriteLine("Define columns (press Enter on an empty name to finish):");

        while (true)
        {
            Console.Write("  Column Name (e.g., 'full_name'): ");
            string colName = Console.ReadLine() ?? "";
            if (string.IsNullOrWhiteSpace(colName))
            {
                break; // User is finished
            }

            Console.Write($"  Data Type for '{colName}' (e.g., 'VARCHAR(100)', 'INT'): ");
            string colType = Console.ReadLine() ?? "";
            if (string.IsNullOrWhiteSpace(colType))
            {
                Console.WriteLine("Data type cannot be empty. Column not added.");
                continue;
            }

            columns.Add(new ColumnDefinition(colName, colType));
        }

        if (columns.Count == 0)
        {
            Console.WriteLine("No columns defined. Table creation cancelled.");
            return;
        }

        Console.WriteLine($"\nCreating table '{tableName}' with {columns.Count} columns...");
        await dbService.CreateTableAsync(tableName, columns, currentUserId);
    }

    /// <summary>
    /// Handles the "Insert Data" action dynamically.
    /// </summary>
    private static async Task HandleInsertDataAsync(DynamicDbService dbService)
    {
        Console.Write("Enter table name to insert into: ");
        string tableName = Console.ReadLine() ?? "";

        var data = new Dictionary<string, object>();
        Console.WriteLine("Enter data as key-value pairs (press Enter on an empty key to finish):");

        while (true)
        {
            Console.Write("  Column Name (key): ");
            string key = Console.ReadLine() ?? "";
            if (string.IsNullOrWhiteSpace(key))
            {
                break; // User is finished
            }

            Console.Write($"  Value for '{key}': ");
            string value = Console.ReadLine() ?? "";

            // Simple type inference. A more robust app might query the schema
            // to do this properly, but this is good for a dynamic tool.
            if (int.TryParse(value, out int intValue))
            {
                data.Add(key, intValue);
            }
            else if (bool.TryParse(value, out bool boolValue))
            {
                data.Add(key, boolValue);
            }
            else if (DateTime.TryParse(value, out DateTime dateValue))
            {
                data.Add(key, dateValue);
            }
            else
            {
                data.Add(key, value);
            }
        }

        if (data.Count == 0)
        {
            Console.WriteLine("No data provided. Insert cancelled.");
            return;
        }

        Console.WriteLine($"\nInserting {data.Count} fields into '{tableName}'...");
        await dbService.InsertDataAsync(tableName, data, currentUserId);
    }

    /// <summary>
    /// Handles the "Query Data" action.
    /// </summary>
    private static async Task HandleQueryDataAsync(DynamicDbService dbService)
    {
        Console.Write("Enter table name to query: ");
        string tableName = Console.ReadLine() ?? "";

        var results = await dbService.QueryDataAsync(tableName, currentUserId);

        Console.WriteLine($"\n--- Query Results for '{tableName}' ---");
        int count = 0;

        // Dapper's 'dynamic' type is perfect here.
        // Each 'row' is a dynamic object.
        foreach (var row in results)
        {
            // Cast to a dictionary to iterate keys/values
            var dict = (IDictionary<string, object>)row;
            var line = string.Join(", ", dict.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
            Console.WriteLine(line);
            count++;
        }

        if (count == 0)
        {
            Console.WriteLine("(No data found or table is empty)");
        }
    }

    /// <summary>
    /// Handles the "Quit" action.
    /// </summary>
    private static Task HandleQuitAsync(DynamicDbService dbService)
    {
        // 'dbService' is unused but required to match the delegate signature
        Console.WriteLine("Exiting application...");
        Environment.Exit(0);
        return Task.CompletedTask; // Unreachable, but satisfies compiler
    }
}