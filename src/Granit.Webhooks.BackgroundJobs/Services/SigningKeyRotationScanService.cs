using Granit.Events;
using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Events;
using Granit.Webhooks.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Webhooks.BackgroundJobs.Services;

/// <summary>
/// Scans signing keys whose <see cref="WebhookSigningKey.ExpiresAt"/> falls within the
/// configured <see cref="WebhooksOptions.RotationLeadTimeDays"/> window and emits a
/// <see cref="WebhookSigningKeyRotationDueEto"/> per match.
/// </summary>
/// <remarks>
/// <para>
/// Idempotent — uses <see cref="WebhookSigningKey.LastRotationNotificationAt"/> for
/// per-key dedupe so the same key is notified at most once per calendar week. After
/// each emission the timestamp is stamped via <see cref="IWebhookSigningKeyWriter.StampRotationNotificationAsync"/>.
/// </para>
/// <para>
/// Already-expired keys are skipped (<c>ExpiresAt &lt;= now</c>): once a key is past its
/// grace period, rotation is too late to prevent disruption — this scanner targets
/// proactive rotation, not post-mortem alerts.
/// </para>
/// <para>
/// Revoked keys (<see cref="WebhookSigningKey.RevokedAt"/> non-null or
/// <see cref="WebhookSigningKey.Status"/> = <see cref="WebhookSigningKeyStatus.Revoked"/>)
/// are excluded by the reader filter.
/// </para>
/// </remarks>
public sealed partial class SigningKeyRotationScanService(
    IWebhookSigningKeyReader keyReader,
    IWebhookSigningKeyWriter keyWriter,
    IDistributedEventBus eventBus,
    IClock clock,
    IOptions<WebhooksOptions> options,
    ILogger<SigningKeyRotationScanService> logger)
{
    /// <summary>Dedupe window: a key gets at most one rotation-due notification per week.</summary>
    private static readonly TimeSpan DedupeWindow = TimeSpan.FromDays(7);

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset now = clock.Now;
        DateTimeOffset cutoff = now.AddDays(options.Value.RotationLeadTimeDays);
        DateTimeOffset notificationDedupeBefore = now - DedupeWindow;

        IReadOnlyList<WebhookSigningKey> expiringKeys = await keyReader
            .GetExpiringSoonAsync(now, cutoff, notificationDedupeBefore, cancellationToken)
            .ConfigureAwait(false);

        if (expiringKeys.Count == 0)
        {
            return;
        }

        Log.ExpiringKeysFound(logger, expiringKeys.Count, options.Value.RotationLeadTimeDays);

        foreach (WebhookSigningKey key in expiringKeys)
        {
            // ExpiresAt is non-null per the reader filter, but defensively coalesce.
            DateTimeOffset expiresAt = key.ExpiresAt ?? cutoff;

            // Publish first, then stamp. If publication fails the stamp is not applied
            // and the next scanner run will re-attempt — preserving at-least-once semantics
            // for the operator alert. Duplicates in the same week are deduped at the next
            // run because the dedupe window is checked against `LastRotationNotificationAt`.
            await eventBus.PublishAsync(
                new WebhookSigningKeyRotationDueEto(key.SubscriptionId, key.Id, expiresAt),
                cancellationToken).ConfigureAwait(false);

            await keyWriter.StampRotationNotificationAsync(
                key.SubscriptionId, key.Id, now, cancellationToken).ConfigureAwait(false);

            Log.RotationDuePublished(logger, key.Id, key.SubscriptionId, expiresAt);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information,
            Message = "Webhook signing-key rotation scanner found {Count} key(s) expiring within {LeadTimeDays} day(s)")]
        public static partial void ExpiringKeysFound(ILogger logger, int count, int leadTimeDays);

        [LoggerMessage(Level = LogLevel.Information,
            Message = "Published WebhookSigningKeyRotationDueEto for key {KeyId} (subscription {SubscriptionId}, expires {ExpiresAt:O})")]
        public static partial void RotationDuePublished(
            ILogger logger,
            Guid keyId,
            Guid subscriptionId,
            DateTimeOffset expiresAt);
    }
}
