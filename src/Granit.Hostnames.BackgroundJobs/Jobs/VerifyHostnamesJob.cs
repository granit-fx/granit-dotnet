using Granit.BackgroundJobs;

namespace Granit.Hostnames.BackgroundJobs.Jobs;

/// <summary>
/// Recurring job that polls for hostnames due for a DNS verification check.
/// Runs every 5 minutes; the actual work is bounded by per-domain
/// <c>NextCheckAt</c> scheduling so each domain is only checked at its
/// backoff-determined time.
/// </summary>
[RecurringJob("*/5 * * * *", "hostnames-verify")]
public sealed record VerifyHostnamesJob : IBackgroundJob;
