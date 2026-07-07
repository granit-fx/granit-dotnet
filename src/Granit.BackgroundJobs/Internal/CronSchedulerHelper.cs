using Cronos;

namespace Granit.BackgroundJobs.Internal;

/// <summary>
/// Shared helpers for cron parsing, next-occurrence computation and job message creation.
/// Single source of truth for the 6-field-then-5-field Cronos parse fallback.
/// </summary>
internal static class CronSchedulerHelper
{
    /// <summary>
    /// Parses a cron expression, trying the 6-field (seconds) format first and
    /// falling back to the standard 5-field format.
    /// </summary>
    /// <exception cref="CronFormatException">When the expression is valid in neither format.</exception>
    internal static CronExpression Parse(string cronExpression)
    {
        try
        {
            return CronExpression.Parse(cronExpression, CronFormat.IncludeSeconds);
        }
        catch (CronFormatException)
        {
            return CronExpression.Parse(cronExpression);
        }
    }

    /// <summary>
    /// Returns the next UTC occurrence of <paramref name="cronExpression"/> strictly after
    /// <paramref name="from"/>, or <c>null</c> when the expression is invalid or produces
    /// no further occurrence.
    /// </summary>
    internal static DateTimeOffset? ComputeNext(string cronExpression, DateTimeOffset from)
    {
        try
        {
            return Parse(cronExpression).GetNextOccurrence(from, TimeZoneInfo.Utc);
        }
        catch (CronFormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Creates an instance of the message type resolved from the assembly-qualified type name.
    /// The resolved type must implement <see cref="IBackgroundJob"/> to prevent arbitrary type
    /// instantiation if the persisted message type is tampered with.
    /// </summary>
    internal static object CreateMessage(string messageType, string jobName)
    {
        var type = Type.GetType(messageType);
        if (type is null)
        {
            throw new InvalidOperationException(
                $"Cannot resolve message type '{messageType}' for job '{jobName}'. " +
                "Ensure the assembly containing the message is loaded.");
        }

        if (!typeof(IBackgroundJob).IsAssignableFrom(type))
        {
            throw new InvalidOperationException(
                $"Type '{type.FullName}' for job '{jobName}' does not implement IBackgroundJob. " +
                "Only registered background job types can be instantiated.");
        }

        return Activator.CreateInstance(type)
            ?? throw new InvalidOperationException(
                $"Cannot instantiate message type '{type.Name}' for job '{jobName}'.");
    }
}
