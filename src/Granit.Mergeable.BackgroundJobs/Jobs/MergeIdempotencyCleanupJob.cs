using Granit.BackgroundJobs;

namespace Granit.Mergeable.BackgroundJobs.Jobs;

/// <summary>
/// Recurring job that deletes <c>granit.merge_idempotency</c> rows older than
/// <c>MergeableOptions.IdempotencyRetention</c> (default 24h). Runs hourly so a freshly
/// raised retention bound takes effect within the hour. Bounded by an explicit batch size
/// inside <see cref="Services.MergeIdempotencyCleanupService"/> to avoid a single sweeper
/// run holding a long-running transaction.
/// </summary>
[RecurringJob("0 * * * *", "mergeable-idempotency-cleanup")]
public sealed record MergeIdempotencyCleanupJob : IBackgroundJob;
