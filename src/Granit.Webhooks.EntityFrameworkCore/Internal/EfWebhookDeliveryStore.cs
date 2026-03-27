using Granit.Guids;
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
internal sealed class EfWebhookDeliveryStore(IDbContextFactory<WebhooksDbContext> contextFactory, IClock clock, IGuidGenerator guidGenerator)
    : IWebhookDeliveryWriter, IWebhookDeliveryReader
{
    public async Task<WebhookDeliveryAttempt?> FindByDeliveryIdAsync(
        Guid deliveryId,
        CancellationToken cancellationToken = default)
    {
        await using WebhooksDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.WebhookDeliveryAttempts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.DeliveryId == deliveryId, cancellationToken).ConfigureAwait(false);
    }

    public async Task RecordSuccessAsync(
        SendWebhookCommand command,
        int httpStatusCode,
        long durationMs,
        string payloadHash,
        string? payload,
        CancellationToken cancellationToken = default)
    {
        await using WebhooksDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        context.WebhookDeliveryAttempts.Add(new WebhookDeliveryAttempt
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

        WebhookSubscription? subscription = await context.WebhookSubscriptions
            .FindAsync([command.SubscriptionId], cancellationToken).ConfigureAwait(false);

        if (subscription is not null)
        {
            subscription.RecordSuccess(clock.Now);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RecordFailureAsync(
        SendWebhookCommand command,
        int? httpStatusCode,
        long durationMs,
        string errorMessage,
        string? payload,
        CancellationToken cancellationToken = default)
    {
        await using WebhooksDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        context.WebhookDeliveryAttempts.Add(new WebhookDeliveryAttempt
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

        WebhookSubscription? subscription = await context.WebhookSubscriptions
            .FindAsync([command.SubscriptionId], cancellationToken).ConfigureAwait(false);

        if (subscription is not null)
        {
            subscription.RecordFailure();
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SuspendSubscriptionAsync(
        Guid subscriptionId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await using WebhooksDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        WebhookSubscription? subscription = await context.WebhookSubscriptions
            .FindAsync([subscriptionId], cancellationToken).ConfigureAwait(false);

        if (subscription is null)
        {
            return;
        }

        subscription.Suspend(clock.Now, "system", reason);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> CountBeforeAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default)
    {
        await using WebhooksDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.WebhookDeliveryAttempts
            .CountAsync(a => a.OccurredAt < cutoff, cancellationToken).ConfigureAwait(false);
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

        await using WebhooksDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.WebhookDeliveryAttempts
            .Where(a => a.OccurredAt < cutoff)
            .OrderBy(a => a.OccurredAt)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }
}
