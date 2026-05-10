using Granit.BackgroundJobs;

namespace Granit.Documents.BackgroundJobs.Jobs;

/// <summary>
/// F9.3 — weekly reconciliation of <c>TenantStorageQuota.UsageBytes</c> against the
/// authoritative sum of <c>DocumentVersion.SizeBytes</c>. Drift normally stays at
/// zero (every write path is atomic), but rare partial-failure modes can leave the
/// quota row out of sync; this job is the long-term safety net.
/// </summary>
[RecurringJob("0 4 * * 0", "documents-quota-recompute")]
public sealed record QuotaRecomputeJob : IBackgroundJob;
