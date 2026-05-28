using Granit.Guids;
using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Internal;
using Granit.Webhooks.Messages;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IWebhookDeliveryWriter"/> and <see cref="IWebhookDeliveryReader"/>.
/// Dispatches through <see cref="WebhooksContextResolver"/> so the same store serves both
/// <c>Shared</c> and <c>Segregated</c> storage modes.
/// </summary>
/// <remarks>
/// ISO 27001 compliance: <see cref="WebhookDeliveryAttempt"/> records are INSERT-only.
/// This store never updates or deletes them outside of the retention purge path.
/// </remarks>
internal sealed class EfWebhookDeliveryStore(
    WebhooksContextResolver resolver,
    IClock clock,
    IGuidGenerator guidGenerator)
    : IWebhookDeliveryWriter, IWebhookDeliveryReader
{
    public async Task<WebhookDeliveryAttempt?> FindByDeliveryIdAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IWebhooksDbContext> contexts = await resolver
            .OpenForUnknownScopeAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (IWebhooksDbContext db in contexts)
            {
                WebhookDeliveryAttempt? hit = await db.WebhookDeliveryAttempts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.DeliveryId == deliveryId, cancellationToken)
                    .ConfigureAwait(false);
                if (hit is not null)
                {
                    return hit;
                }
            }

            return null;
        }
        finally
        {
            await DisposeAllAsync(contexts).ConfigureAwait(false);
        }
    }

    public Task RecordSuccessAsync(
        SendWebhookCommand command,
        int httpStatusCode,
        long durationMs,
        string payloadHash,
        string? payload,
        CancellationToken cancellationToken = default) =>
        RecordAttemptAsync(
            command,
            buildAttempt: id => new WebhookDeliveryAttempt
            {
                Id = id,
                DeliveryId = command.DeliveryId,
                SubscriptionId = command.SubscriptionId,
                TenantId = command.Envelope.TenantId,
                EventType = command.Envelope.EventType,
                TargetUrl = command.TargetUrl,
                HttpStatusCode = httpStatusCode,
                PayloadHash = payloadHash,
                Payload = payload,
                OccurredAt = clock.Now,
                DurationMs = durationMs,
                IsSuccess = true,
            },
            transition: subscription => subscription.RecordSuccess(clock.Now),
            cancellationToken);

    public Task RecordFailureAsync(
        SendWebhookCommand command,
        int? httpStatusCode,
        long durationMs,
        string errorMessage,
        string? payload,
        CancellationToken cancellationToken = default) =>
        RecordAttemptAsync(
            command,
            buildAttempt: id => new WebhookDeliveryAttempt
            {
                Id = id,
                DeliveryId = command.DeliveryId,
                SubscriptionId = command.SubscriptionId,
                TenantId = command.Envelope.TenantId,
                EventType = command.Envelope.EventType,
                TargetUrl = command.TargetUrl,
                HttpStatusCode = httpStatusCode,
                PayloadHash = string.Empty,
                Payload = payload,
                OccurredAt = clock.Now,
                DurationMs = durationMs,
                ErrorMessage = errorMessage.Length > 2000 ? errorMessage[..2000] : errorMessage,
                IsSuccess = false,
            },
            transition: subscription => subscription.RecordFailure(),
            cancellationToken);

    public async Task SuspendSubscriptionAsync(
        Guid subscriptionId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IWebhooksDbContext> contexts = await resolver
            .OpenForUnknownScopeAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (IWebhooksDbContext db in contexts)
            {
                WebhookSubscription? subscription = await db.WebhookSubscriptions
                    .FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken)
                    .ConfigureAwait(false);

                if (subscription is null)
                {
                    continue;
                }

                subscription.Suspend(clock.Now, "system", reason);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
        }
        finally
        {
            await DisposeAllAsync(contexts).ConfigureAwait(false);
        }
    }

    public async Task<int> CountBeforeAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IWebhooksDbContext> contexts = await resolver
            .OpenAllAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            int total = 0;
            foreach (IWebhooksDbContext db in contexts)
            {
                total += await db.WebhookDeliveryAttempts
                    .CountAsync(a => a.OccurredAt < cutoff, cancellationToken)
                    .ConfigureAwait(false);
            }

            return total;
        }
        finally
        {
            await DisposeAllAsync(contexts).ConfigureAwait(false);
        }
    }

    public async Task<int> DeleteBeforeAsync(
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        // ISO 27001: enforce 3-year minimum retention for delivery audit records.
        DateTimeOffset earliestAllowed = clock.Now - WebhooksConstants.MinAuditRetention;
        if (cutoff > earliestAllowed)
        {
            throw new InvalidOperationException(
                $"Cannot delete delivery records newer than {WebhooksConstants.MinAuditRetention.TotalDays:F0} days. "
                + $"Requested cutoff: {cutoff:O}, earliest allowed: {earliestAllowed:O}.");
        }

        IReadOnlyList<IWebhooksDbContext> contexts = await resolver
            .OpenAllAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            int totalDeleted = 0;
            foreach (IWebhooksDbContext db in contexts)
            {
                int deleted = await db.WebhookDeliveryAttempts
                    .Where(a => a.OccurredAt < cutoff)
                    .OrderBy(a => a.OccurredAt)
                    .Take(batchSize)
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);
                totalDeleted += deleted;
            }

            return totalDeleted;
        }
        finally
        {
            await DisposeAllAsync(contexts).ConfigureAwait(false);
        }
    }

    private async Task RecordAttemptAsync(
        SendWebhookCommand command,
        Func<Guid, WebhookDeliveryAttempt> buildAttempt,
        Action<WebhookSubscription> transition,
        CancellationToken cancellationToken)
    {
        // The delivery attempt is co-located with its parent subscription. Open by known
        // scope (the envelope carries TenantId — null for host SIEM forwards), insert the
        // attempt, transition the subscription state machine in the same context, save.
        await using IWebhooksDbContext db = await resolver
            .OpenForScopeAsync(command.Envelope.TenantId, cancellationToken).ConfigureAwait(false);

        db.WebhookDeliveryAttempts.Add(buildAttempt(guidGenerator.Create()));

        WebhookSubscription? subscription = await db.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == command.SubscriptionId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is not null)
        {
            transition(subscription);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask DisposeAllAsync(IReadOnlyList<IWebhooksDbContext> contexts)
    {
        foreach (IWebhooksDbContext db in contexts)
        {
            await db.DisposeAsync().ConfigureAwait(false);
        }
    }
}
