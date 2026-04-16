using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore.ExceptionHandling;

/// <summary>
/// Provider-agnostic helper for classifying <see cref="DbUpdateException"/> causes.
/// </summary>
/// <remarks>
/// Uses reflection to read provider-specific error properties (Npgsql, SqlClient)
/// without taking a hard dependency. Falls back to message phrase matching for
/// SQLite, MySQL, and other providers.
/// </remarks>
public static class DbUpdateExceptionHelper
{
    /// <summary>
    /// Returns <see langword="true"/> when the exception represents a unique-constraint
    /// violation (duplicate key), regardless of the underlying database provider.
    /// </summary>
    /// <remarks>
    /// Supported providers:
    /// <list type="bullet">
    ///   <item><description>PostgreSQL — <c>SqlState = "23505"</c> (unique_violation)</description></item>
    ///   <item><description>SQL Server — <c>Number</c> 2601 (unique index) or 2627 (unique constraint)</description></item>
    ///   <item><description>MySQL — <c>Number</c> 1062 (ER_DUP_ENTRY)</description></item>
    ///   <item><description>SQLite / others — message phrase matching</description></item>
    /// </list>
    /// </remarks>
    public static bool IsDuplicateKeyException(DbUpdateException ex)
    {
        if (ex.InnerException is null)
        {
            return false;
        }

        Type innerType = ex.InnerException.GetType();

        // PostgreSQL (Npgsql): SqlState = "23505" (unique_violation)
        if (innerType.GetProperty("SqlState")?.GetValue(ex.InnerException) is "23505")
        {
            return true;
        }

        // SQL Server (Microsoft.Data.SqlClient) or MySQL (MySqlConnector): Number property
        if (innerType.GetProperty("Number")?.GetValue(ex.InnerException) is int number
            && number is 2601 or 2627 or 1062)
        {
            return true;
        }

        // Fallback: phrase matching for SQLite and other providers
        string? message = ex.InnerException.Message;
        return message?.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true
            || message?.Contains("unique constraint", StringComparison.OrdinalIgnoreCase) == true
            || message?.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) == true;
    }
}
