using Granit.BackgroundJobs;

namespace Granit.Taxonomy.BackgroundJobs.Jobs;

/// <summary>
/// Recurring job that sweeps orphan <c>TagAssignment</c> and
/// <c>CategoryAssignment</c> rows nightly at 03:00 UTC. Complements the
/// synchronous T5.1 cleanup by catching targets deleted via raw SQL or by
/// modules that didn't opt into <c>IEmitEntityLifecycleEvents</c>.
/// </summary>
[RecurringJob("0 3 * * *", "taxonomy-orphan-assignment-cleanup")]
public sealed record OrphanAssignmentCleanupJob : IBackgroundJob;
