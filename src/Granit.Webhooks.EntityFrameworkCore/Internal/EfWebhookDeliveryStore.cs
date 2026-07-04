using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Internal;
using Granit.Webhooks.Messages;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IWebhookDeliveryWriter"/> and <see cref="IWebhookDeliveryReader"/>
/// backed by PostgreSQL.
/// </summary>
/// <remarks>
/// ISO 27001 compliance: <see cref="WebhookDeliveryAttempt"/> records are INSERT-only.
/// This store never updates or deletes them.
/// </remarks>
internal sealed class EfWebhookDeliveryStore(
    IDbContextFactory<WebhooksDbContext> contextFactory,
    ICurrentTenant currentTenant,
    IClock clock,
    IGuidGenerator guidGenerator)
    : EfStoreBase<WebhookDeliveryAttempt, WebhooksDbContext>(contextFactory, currentTenant),
      IWebhookDeliveryWriter, IWebhookDeliveryReader
{
    public Task<WebhookDeliveryAttempt?> FindByDeliveryIdAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default) =>
        ReadAsync(db => db.WebhookDeliveryAttempts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.DeliveryId == deliveryId, cancellationToken),
        cancellationToken);

    public Task RecordSuccessAsync(
        SendWebhookCommand command,
        int httpStatusCode,
        long durationMs,
        string payloadHash,
        string? payload,
        CancellationToken cancellationToken = default) =>
        WriteAsync(async db =>
        {
            db.WebhookDeliveryAttempts.Add(new WebhookDeliveryAttempt
            {
                Id = guidGenerator.Create(),
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
            });

            WebhookSubscription? subscription = await db.WebhookSubscriptions
                .FirstOrDefaultAsync(s => s.Id == command.SubscriptionId, cancellationToken).ConfigureAwait(false);

            subscription?.RecordSuccess(clock.Now);
        }, cancellationToken);

    public Task RecordFailureAsync(
        SendWebhookCommand command,
        int? httpStatusCode,
        long durationMs,
        string errorMessage,
        string? payload,
        CancellationToken cancellationToken = default) =>
        WriteAsync(async db =>
        {
            db.WebhookDeliveryAttempts.Add(new WebhookDeliveryAttempt
            {
                Id = guidGenerator.Create(),
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
            });

            WebhookSubscription? subscription = await db.WebhookSubscriptions
                .FirstOrDefaultAsync(s => s.Id == command.SubscriptionId, cancellationToken).ConfigureAwait(false);

            subscription?.RecordFailure();
        }, cancellationToken);

    public Task SuspendSubscriptionAsync(
        Guid subscriptionId,
        string reason,
        CancellationToken cancellationToken = default) =>
        WriteAsync(async db =>
        {
            WebhookSubscription? subscription = await db.WebhookSubscriptions
                .FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken).ConfigureAwait(false);

            if (subscription is null)
            {
                return;
            }

            subscription.Suspend(clock.Now, "system", reason);
        }, cancellationToken);

    public Task<int> CountBeforeAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default) =>
        CountAsync(a => a.OccurredAt < cutoff, cancellationToken);

    public Task<int> DeleteBeforeAsync(
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

        return WriteAsync(db => db.WebhookDeliveryAttempts
            .Where(a => a.OccurredAt < cutoff)
            .OrderBy(a => a.OccurredAt)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken),
        cancellationToken);
    }
}
