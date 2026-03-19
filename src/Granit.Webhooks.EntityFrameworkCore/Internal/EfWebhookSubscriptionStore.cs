using System.Security.Cryptography;
using Granit.Core.Exceptions;
using Granit.Guids;
using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IWebhookSubscriptionReader"/> and
/// <see cref="IWebhookSubscriptionWriter"/> backed by PostgreSQL.
/// </summary>
internal sealed class EfWebhookSubscriptionStore(
    IDbContextFactory<WebhooksDbContext> contextFactory,
    IGuidGenerator guidGenerator,
    IWebhookSecretProtector secretProtector,
    IClock clock)
    : IWebhookSubscriptionReader, IWebhookSubscriptionWriter
{
    public async Task<IReadOnlyList<WebhookSubscription>> GetActiveSubscriptionsAsync(
        string eventType,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        await using WebhooksDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.WebhookSubscriptions
            .Where(s => s.Status == WebhookSubscriptionStatus.Active
                     && s.EventType == eventType
                     && (s.TenantId == null || s.TenantId == tenantId))
            .AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<WebhookSubscription?> FindByIdAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        await using WebhooksDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.WebhookSubscriptions.FindAsync([subscriptionId], cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<WebhookSubscription>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using WebhooksDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await context.WebhookSubscriptions
            .AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<WebhookSubscriptionCreatedResult> CreateAsync(
        string targetUrl,
        string eventType,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        string plainSecret = GenerateSigningSecret();
        string protectedSecret = await secretProtector
            .ProtectAsync(plainSecret, cancellationToken)
            .ConfigureAwait(false);

        var subscription = WebhookSubscription.Create(
            guidGenerator.Create(),
            targetUrl,
            eventType,
            protectedSecret,
            tenantId);

        await using WebhooksDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.WebhookSubscriptions.Add(subscription);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new WebhookSubscriptionCreatedResult(subscription, plainSecret);
    }

    public async Task UpdateTargetUrlAsync(
        Guid subscriptionId,
        string targetUrl,
        CancellationToken cancellationToken = default)
    {
        await using WebhooksDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        WebhookSubscription subscription = await FindOrThrowAsync(context, subscriptionId, cancellationToken).ConfigureAwait(false);
        subscription.UpdateTargetUrl(targetUrl);

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ActivateAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        await using WebhooksDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        WebhookSubscription subscription = await FindOrThrowAsync(context, subscriptionId, cancellationToken).ConfigureAwait(false);

        if (subscription.Status != WebhookSubscriptionStatus.Suspended)
        {
            throw new ConflictException(
                "Webhooks:InvalidStateTransition",
                $"Cannot activate a subscription with status '{subscription.Status}'. Only 'Suspended' subscriptions can be activated.");
        }

        subscription.Activate();
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SuspendAsync(
        Guid subscriptionId,
        string suspendedBy,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await using WebhooksDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        WebhookSubscription subscription = await FindOrThrowAsync(context, subscriptionId, cancellationToken).ConfigureAwait(false);

        if (subscription.Status != WebhookSubscriptionStatus.Active)
        {
            throw new ConflictException(
                "Webhooks:InvalidStateTransition",
                $"Cannot suspend a subscription with status '{subscription.Status}'. Only 'Active' subscriptions can be suspended.");
        }

        subscription.Suspend(clock.Now, suspendedBy, reason);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeactivateAsync(
        Guid subscriptionId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        await using WebhooksDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        WebhookSubscription subscription = await FindOrThrowAsync(context, subscriptionId, cancellationToken).ConfigureAwait(false);

        if (subscription.Status == WebhookSubscriptionStatus.Deactivated)
        {
            throw new ConflictException(
                "Webhooks:AlreadyDeactivated",
                "Subscription is already deactivated.");
        }

        subscription.Deactivate(reason);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        await using WebhooksDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        WebhookSubscription subscription = await FindOrThrowAsync(context, subscriptionId, cancellationToken).ConfigureAwait(false);

        context.WebhookSubscriptions.Remove(subscription);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> RotateSecretAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        await using WebhooksDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        WebhookSubscription subscription = await FindOrThrowAsync(context, subscriptionId, cancellationToken).ConfigureAwait(false);

        string plainSecret = GenerateSigningSecret();
        string protectedSecret = await secretProtector
            .ProtectAsync(plainSecret, cancellationToken)
            .ConfigureAwait(false);

        subscription.RotateSecret(protectedSecret);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return plainSecret;
    }

    private static async Task<WebhookSubscription> FindOrThrowAsync(
        WebhooksDbContext context,
        Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        WebhookSubscription? subscription = await context.WebhookSubscriptions
            .FindAsync([subscriptionId], cancellationToken).ConfigureAwait(false);

        return subscription ?? throw new EntityNotFoundException(typeof(WebhookSubscription), subscriptionId);
    }

    private static string GenerateSigningSecret() =>
        $"whsec_{Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()}";
}
