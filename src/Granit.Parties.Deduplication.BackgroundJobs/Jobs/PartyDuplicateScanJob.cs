using Granit.BackgroundJobs;

namespace Granit.Parties.Deduplication.BackgroundJobs.Jobs;

/// <summary>
/// Recurring job that scans every active tenant for potential duplicate parties and
/// materialises the findings into the <c>parties_duplicate_candidates</c> review table.
/// Runs daily at 03:00 to amortise the load outside business hours.
/// </summary>
[RecurringJob("0 3 * * *", "parties-duplicate-scan")]
public sealed record PartyDuplicateScanJob : IBackgroundJob;
