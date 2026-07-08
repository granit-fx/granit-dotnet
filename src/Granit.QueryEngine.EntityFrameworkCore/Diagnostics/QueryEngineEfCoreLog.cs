using Microsoft.Extensions.Logging;

namespace Granit.QueryEngine.EntityFrameworkCore.Diagnostics;

/// <summary>
/// High-performance structured log messages for the QueryEngine EF Core engine.
/// Uses source-generated <see cref="LoggerMessageAttribute"/> to avoid boxing and string parsing.
/// Event ids are stable (8200-8299 range reserved for Granit.QueryEngine.EntityFrameworkCore)
/// so log-based alerting keys don't shift when methods are reordered.
/// </summary>
internal static partial class QueryEngineEfCoreLog
{
    [LoggerMessage(
        EventId = 8200,
        Level = LogLevel.Debug,
        Message = "Filter value conversion failed for field '{Field}' to {TargetType}")]
    public static partial void FilterValueConversionFailed(
        ILogger logger, string field, string targetType, Exception exception);

    [LoggerMessage(
        EventId = 8201,
        Level = LogLevel.Debug,
        Message = "Filter field '{Field}' not found on entity type {EntityType}")]
    public static partial void FilterFieldNotFound(
        ILogger logger, string field, string entityType);

    [LoggerMessage(
        EventId = 8202,
        Level = LogLevel.Debug,
        Message = "Cursor decode failed")]
    public static partial void CursorDecodeFailed(
        ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 8203,
        Level = LogLevel.Warning,
        Message = "Stream limit reached for entity type {EntityType}: {Limit} items — results are truncated")]
    public static partial void StreamLimitReached(
        ILogger logger, string entityType, int limit);

    [LoggerMessage(
        EventId = 8204,
        Level = LogLevel.Warning,
        Message = "Substring filter (contains/startsWith/endsWith) on field '{Field}' of non-string type {ColumnType} is ignored — EF Core cannot translate LIKE over a value-object/non-string column. See issue #2767.")]
    public static partial void SubstringFilterOnNonStringColumnIgnored(
        ILogger logger, string field, string columnType);

    [LoggerMessage(
        EventId = 8205,
        Level = LogLevel.Warning,
        Message = "A query definition enables cursor pagination but QueryEngineOptions.CursorHmacKey is not configured — cursors are unsigned and forgeable (CWE-565). Configure a Base64 256-bit key in production. This warning is emitted once per process.")]
    public static partial void UnsignedCursorPagination(ILogger logger);
}
