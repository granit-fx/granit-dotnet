namespace Granit.Observability.AI;

/// <summary>
/// Represents a single log entry for AI analysis.
/// </summary>
/// <param name="Timestamp">When the log entry was recorded.</param>
/// <param name="Level">Log level (e.g. "Error", "Warning", "Information").</param>
/// <param name="Message">The log message text.</param>
/// <param name="Exception">Optional exception details associated with the log entry.</param>
public sealed record LogEntry(
    DateTimeOffset Timestamp,
    string Level,
    string Message,
    string? Exception);
