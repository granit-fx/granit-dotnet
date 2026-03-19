namespace Granit.BackgroundJobs;

/// <summary>
/// Marks a message type as a Wolverine-scheduled recurring job.
/// </summary>
/// <remarks>
/// <para>
/// Apply this attribute to a plain message class to declare it as a recurring background job.
/// <see cref="Granit.BackgroundJobs.Internal.RecurringJobPolicy"/> detects decorated message types
/// at startup and automatically injects
/// <see cref="Granit.BackgroundJobs.Internal.RecurringJobSchedulingMiddleware"/> into their Wolverine
/// handler chains. The rescheduling is performed atomically inside the Outbox transaction, preventing
/// both duplicates and message loss in multi-node clusters.
/// </para>
/// <para>
/// The job is seeded into the persistent store on first startup via
/// <see cref="Granit.BackgroundJobs.Internal.RecurringJobDiscovery"/>.
/// </para>
/// <example>
/// <code>
/// [RecurringJob("0 * * * *", "my-module-hourly-cleanup")]
/// public sealed record HourlyCleanupJob : IBackgroundJob;
///
/// internal static partial class HourlyCleanupHandler
/// {
///     public static async Task HandleAsync(HourlyCleanupJob job, IMyService service)
///         => await service.CleanupAsync();
///     // Rescheduling is injected automatically — no code needed here.
/// }
/// </code>
/// </example>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RecurringJobAttribute : Attribute
{
    /// <summary>
    /// Initializes a new <see cref="RecurringJobAttribute"/>.
    /// </summary>
    /// <param name="cronExpression">
    /// Standard cron expression (5 or 6 fields, parsed by Cronos).
    /// Example: <c>"0 * * * *"</c> (every hour at minute 0).
    /// </param>
    /// <param name="name">
    /// Unique, stable identifier for the job. Used as the primary key in the persistent store.
    /// Must be kebab-case, lowercase, non-empty.
    /// </param>
    public RecurringJobAttribute(string cronExpression, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cronExpression);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        CronExpression = cronExpression;
        Name = name;
    }

    /// <summary>Gets the cron expression defining the recurrence schedule.</summary>
    public string CronExpression { get; }

    /// <summary>Gets the unique, stable identifier of the job.</summary>
    public string Name { get; }
}
