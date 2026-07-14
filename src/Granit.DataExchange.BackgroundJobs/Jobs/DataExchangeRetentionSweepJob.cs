using Granit.BackgroundJobs;

namespace Granit.DataExchange.BackgroundJobs.Jobs;

/// <summary>
/// Recurring GDPR retention sweep for <c>Granit.DataExchange</c>: purges expired import/export
/// files and job records, and recovers jobs stranded in a non-terminal executing state.
/// Runs daily at 3:00 AM.
/// </summary>
[RecurringJob("0 3 * * *", "data-exchange-retention-sweep")]
public sealed record DataExchangeRetentionSweepJob : IBackgroundJob;
