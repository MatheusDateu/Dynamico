using System.Text.RegularExpressions;
namespace Dynamico.Base;
public static class SqlSafeBuilder
{
    // A strict regex that only allows letters, numbers, and underscores.
    // It must not start with a number and must have a limited length.
    private static readonly Regex IdentifierRegex =
        new Regex(@"^[a-zA-Z_][a-zA-Z0-9_]{0,50}$");

    // A list of common SQL reserved keywords. This is not exhaustive
    // but covers the most dangerous ones (add more as needed).
    private static readonly HashSet<string> ReservedKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "SELECT", "INSERT", "UPDATE", "DELETE", "CREATE", "DROP", "TABLE",
        "ALTER", "WHERE", "FROM", "USER", "GRANT", "PUBLIC", "GROUP", "BY"
    };

    /// <summary>
    /// Validates an SQL identifier (table or column name).
    /// Throws an exception if the identifier is invalid or unsafe.
    /// </summary>
    /// <param name="identifier">The identifier to validate.</param>
    /// <returns>The validated identifier (Quoted).</returns>
    public static string QuoteIdentifier(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Identifier cannot be null or empty.");
        }

        if (!IdentifierRegex.IsMatch(identifier))
        {
            throw new ArgumentException($"Invalid identifier format: '{identifier}'. Only letters, numbers, and underscores are allowed, and it must not start with a number.");
        }

        if (ReservedKeywords.Contains(identifier))
        {
            throw new ArgumentException($"Invalid identifier: '{identifier}' is a reserved SQL keyword.");
        }

        // Return the identifier safely quoted for PostgreSQL
        return $"\"{identifier}\"";
    }

    /// <summary>
    /// Validates a data type. This is less critical for injection
    /// but still good practice to control.
    /// </summary>
    public static string ValidateDataType(string dataType)
    {
        // For a real application, you would check against a specific list
        // e.g., "VARCHAR(100)", "INT", "BOOLEAN", "TIMESTAMPTZ"
        // For this example, we'll keep it simple if it's one word.
        if (string.IsNullOrWhiteSpace(dataType) || !Regex.IsMatch(dataType, @"^[a-zA-Z0-9\(\)]+$"))
        {
            throw new ArgumentException($"Invalid data type: {dataType}");
        }

        // This is safe as we validated it
        return dataType;
    }
}