using System.Security.Cryptography;
using Granit.Domain.ValueObjects;
using Granit.Exceptions;
using Granit.Guids;
using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
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
    : EfStoreBase<WebhookSubscription, WebhooksDbContext>(contextFactory),
      IWebhookSubscriptionReader, IWebhookSubscriptionWriter
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<WebhookSubscription>> GetActiveSubscriptionsAsync(
        string eventType,
        Guid? tenantId,
        CancellationToken cancellationToken = default) =>
        ListAsync(
            Spec.For<WebhookSubscription>()
                .Where(s => s.Status == WebhookSubscriptionStatus.Active
                         && s.EventType == eventType
                         && (s.TenantId == null || s.TenantId == tenantId)),
            cancellationToken);

    /// <inheritdoc/>
    public new Task<WebhookSubscription?> FindByIdAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default) =>
        base.FindByIdAsync(subscriptionId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<WebhookSubscription>> GetAllAsync(CancellationToken cancellationToken = default) =>
        ListAsync(Spec.For<WebhookSubscription>(), cancellationToken);

    /// <inheritdoc/>
    public async Task<WebhookSubscriptionCreatedResult> CreateAsync(
        HttpsUrl targetUrl,
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

        await AddAsync(subscription, cancellationToken).ConfigureAwait(false);

        return new WebhookSubscriptionCreatedResult(subscription, plainSecret);
    }

    /// <inheritdoc/>
    public Task UpdateTargetUrlAsync(
        Guid subscriptionId,
        HttpsUrl targetUrl,
        CancellationToken cancellationToken = default) =>
        WriteAsync(async db =>
        {
            WebhookSubscription subscription = await FindOrThrowAsync(db, subscriptionId, cancellationToken).ConfigureAwait(false);
            subscription.UpdateTargetUrl(targetUrl);
        }, cancellationToken);

    /// <inheritdoc/>
    public Task ActivateAsync(Guid subscriptionId, CancellationToken cancellationToken = default) =>
        WriteAsync(async db =>
        {
            WebhookSubscription subscription = await FindOrThrowAsync(db, subscriptionId, cancellationToken).ConfigureAwait(false);
            subscription.Activate();
        }, cancellationToken);

    /// <inheritdoc/>
    public Task SuspendAsync(
        Guid subscriptionId,
        string suspendedBy,
        string reason,
        CancellationToken cancellationToken = default) =>
        WriteAsync(async db =>
        {
            WebhookSubscription subscription = await FindOrThrowAsync(db, subscriptionId, cancellationToken).ConfigureAwait(false);
            subscription.Suspend(clock.Now, suspendedBy, reason);
        }, cancellationToken);

    /// <inheritdoc/>
    public Task DeactivateAsync(
        Guid subscriptionId,
        string reason,
        CancellationToken cancellationToken = default) =>
        WriteAsync(async db =>
        {
            WebhookSubscription subscription = await FindOrThrowAsync(db, subscriptionId, cancellationToken).ConfigureAwait(false);
            subscription.Deactivate(reason);
        }, cancellationToken);

    /// <inheritdoc/>
    public Task DeleteAsync(Guid subscriptionId, CancellationToken cancellationToken = default) =>
        WriteAsync(async db =>
        {
            WebhookSubscription subscription = await FindOrThrowAsync(db, subscriptionId, cancellationToken).ConfigureAwait(false);
            db.WebhookSubscriptions.Remove(subscription);
        }, cancellationToken);

    /// <inheritdoc/>
    public async Task<string> RotateSecretAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        string plainSecret = GenerateSigningSecret();
        string protectedSecret = await secretProtector
            .ProtectAsync(plainSecret, cancellationToken)
            .ConfigureAwait(false);

        await WriteAsync(async db =>
        {
            WebhookSubscription subscription = await FindOrThrowAsync(db, subscriptionId, cancellationToken).ConfigureAwait(false);
            subscription.RotateSecret(protectedSecret);
        }, cancellationToken).ConfigureAwait(false);

        return plainSecret;
    }

    private static async Task<WebhookSubscription> FindOrThrowAsync(
        WebhooksDbContext context,
        Guid subscriptionId,
        CancellationToken cancellationToken)
    {
        WebhookSubscription? subscription = await context.WebhookSubscriptions
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken).ConfigureAwait(false);

        return subscription ?? throw new EntityNotFoundException(typeof(WebhookSubscription), subscriptionId);
    }

    private static string GenerateSigningSecret() =>
        $"whsec_{Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()}";
}
