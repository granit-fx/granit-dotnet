using Microsoft.Extensions.Logging;

namespace Granit.QueryEngine.EntityFrameworkCore.Diagnostics;

/// <summary>
/// High-performance structured log messages for the QueryEngine EF Core engine.
/// Uses source-generated <see cref="LoggerMessageAttribute"/> to avoid boxing and string parsing.
/// </summary>
internal static partial class QueryEngineEfCoreLog
{
    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Filter value conversion failed for field '{Field}': cannot convert '{Value}' to {TargetType}")]
    public static partial void FilterValueConversionFailed(
        ILogger logger, string field, string value, string targetType, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Filter field '{Field}' not found on entity type {EntityType}")]
    public static partial void FilterFieldNotFound(
        ILogger logger, string field, string entityType);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Cursor decode failed for cursor '{Cursor}'")]
    public static partial void CursorDecodeFailed(
        ILogger logger, string cursor, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Stream limit reached for entity type {EntityType}: {Limit} items — results are truncated")]
    public static partial void StreamLimitReached(
        ILogger logger, string entityType, int limit);
}
