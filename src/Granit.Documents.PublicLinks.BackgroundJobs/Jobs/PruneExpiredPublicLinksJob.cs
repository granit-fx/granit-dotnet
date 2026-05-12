using Granit.BackgroundJobs;

namespace Granit.Documents.PublicLinks.BackgroundJobs.Jobs;

/// <summary>
/// Recurring cleanup that physically deletes <c>DocumentPublicLink</c> rows whose
/// <c>ExpiresAt</c> (or <c>RevokedAt</c>) is older than the configured retention
/// buffer (default 90 days). The matching <c>tenant_expires</c> index keeps the
/// scan cheap.
/// </summary>
/// <remarks>
/// Runs daily at 03:00 UTC — off business hours for every supported region.
/// </remarks>
[RecurringJob("0 3 * * *", "documents-public-links-prune-expired")]
public sealed record PruneExpiredPublicLinksJob : IBackgroundJob;
