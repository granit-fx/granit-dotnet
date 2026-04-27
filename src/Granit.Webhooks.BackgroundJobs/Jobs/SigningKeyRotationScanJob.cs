using Granit.BackgroundJobs;

namespace Granit.Webhooks.BackgroundJobs.Jobs;

/// <summary>
/// Recurring scanner that emits a <see cref="Events.WebhookSigningKeyRotationDueEto"/>
/// for every <see cref="Domain.WebhookSigningKey"/> whose <c>ExpiresAt</c> falls within
/// the configured <see cref="Options.WebhooksOptions.RotationLeadTimeDays"/> window.
/// </summary>
/// <remarks>
/// Runs daily at 07:00 UTC. Idempotent — per-key dedupe via
/// <see cref="Domain.WebhookSigningKey.LastRotationNotificationAt"/>; the same key is
/// notified at most once per calendar week.
/// </remarks>
[RecurringJob("0 7 * * *", "webhooks-key-rotation-scan")]
public sealed record SigningKeyRotationScanJob : IBackgroundJob;
