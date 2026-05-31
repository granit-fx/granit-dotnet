using System.Security.Cryptography;
using Granit.Domain.ValueObjects;
using Granit.Exceptions;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Internal;
using Granit.Webhooks.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Granit.Webhooks.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IWebhookSubscriptionReader"/>,
/// <see cref="IWebhookSubscriptionWriter"/>, <see cref="IWebhookSigningKeyReader"/>, and
/// <see cref="IWebhookSigningKeyWriter"/> backed by PostgreSQL.
/// </summary>
internal sealed class EfWebhookSubscriptionStore(
    IDbContextFactory<WebhooksDbContext> contextFactory,
    ICurrentTenant currentTenant,
    IGuidGenerator guidGenerator,
    IWebhookSecretProtector secretProtector,
    IClock clock,
    IOptions<WebhooksOptions> options)
    : EfStoreBase<WebhookSubscription, WebhooksDbContext>(contextFactory, currentTenant),
      IWebhookSubscriptionReader,
      IWebhookSubscriptionWriter,
      IWebhookSigningKeyReader,
      IWebhookSigningKeyWriter
{
    // Locally-held copy of the factory so we can spin up DbContexts for queries that need
    // .Include(s => s.SigningKeys) without going through the EfStoreBase Query() pipeline.
    // The base class already captures contextFactory; using the parameter directly here would
    // raise CS9107 (double capture).
    private readonly IDbContextFactory<WebhooksDbContext> _contextFactory = contextFactory;
    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebhookSubscription>> GetActiveSubscriptionsAsync(
        string eventType,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        await using WebhooksDbContext db = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<WebhookSubscription> subs = await db.WebhookSubscriptions
            .Include(s => s.SigningKeys)
            .Where(s => s.Status == WebhookSubscriptionStatus.Active
                     && s.EventType == eventType
                     && (s.TenantId == null || s.TenantId == tenantId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return subs;
    }

    /// <inheritdoc/>
    public new async Task<WebhookSubscription?> FindByIdAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        await using WebhooksDbContext db = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.WebhookSubscriptions
            .Include(s => s.SigningKeys)
            .FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebhookSubscription>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using WebhooksDbContext db = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<WebhookSubscription> subs = await db.WebhookSubscriptions
            .Include(s => s.SigningKeys)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return subs;
    }

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
            guidGenerator.Create(),
            protectedSecret,
            clock.Now,
            tenantId,
            signingSecretHint: WebhookSecretHint.From(plainSecret));

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
    public async Task<WebhookSigningKeyRotatedResult> RotateSigningKeyAsync(
        Guid subscriptionId,
        TimeSpan? retiredKeyGracePeriod = null,
        CancellationToken cancellationToken = default)
    {
        string plainSecret = GenerateSigningSecret();
        string protectedSecret = await secretProtector
            .ProtectAsync(plainSecret, cancellationToken)
            .ConfigureAwait(false);

        TimeSpan grace = retiredKeyGracePeriod ?? options.Value.RetiredKeyGracePeriod;
        Guid newKeyId = guidGenerator.Create();

        string hint = WebhookSecretHint.From(plainSecret);

        await WriteAsync(async db =>
        {
            WebhookSubscription subscription = await db.WebhookSubscriptions
                .Include(s => s.SigningKeys)
                .FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new EntityNotFoundException(typeof(WebhookSubscription), subscriptionId);

            subscription.RotateSigningKey(newKeyId, protectedSecret, clock.Now, grace, newSigningSecretHint: hint);
        }, cancellationToken).ConfigureAwait(false);

        return new WebhookSigningKeyRotatedResult(newKeyId, plainSecret);
    }

    /// <inheritdoc/>
    public Task RevokeSigningKeyAsync(
        Guid subscriptionId,
        Guid keyId,
        CancellationToken cancellationToken = default) =>
        WriteAsync(async db =>
        {
            WebhookSubscription subscription = await db.WebhookSubscriptions
                .Include(s => s.SigningKeys)
                .FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new EntityNotFoundException(typeof(WebhookSubscription), subscriptionId);

            bool revoked = subscription.RevokeSigningKey(keyId, clock.Now);
            if (!revoked)
            {
                throw new EntityNotFoundException(typeof(WebhookSigningKey), keyId);
            }
        }, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebhookSigningKey>> GetForSubscriptionAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        await using WebhooksDbContext db = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<WebhookSigningKey> keys = await db.WebhookSigningKeys
            .Where(k => k.SubscriptionId == subscriptionId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return keys;
    }

    async Task<WebhookSigningKey?> IWebhookSigningKeyReader.FindByIdAsync(
        Guid keyId,
        CancellationToken cancellationToken)
    {
        await using WebhooksDbContext db = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        return await db.WebhookSigningKeys
            .FirstOrDefaultAsync(k => k.Id == keyId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebhookSigningKey>> GetExpiringSoonAsync(
        DateTimeOffset now,
        DateTimeOffset cutoff,
        DateTimeOffset notificationDedupeBefore,
        CancellationToken cancellationToken = default)
    {
        await using WebhooksDbContext db = await _contextFactory
            .CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        List<WebhookSigningKey> keys = await db.WebhookSigningKeys
            .Where(k => (k.Status == WebhookSigningKeyStatus.Active
                      || k.Status == WebhookSigningKeyStatus.Retired)
                     && k.RevokedAt == null
                     && k.ExpiresAt != null
                     && k.ExpiresAt > now
                     && k.ExpiresAt <= cutoff
                     && (k.LastRotationNotificationAt == null
                         || k.LastRotationNotificationAt < notificationDedupeBefore))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return keys;
    }

    /// <inheritdoc/>
    public Task StampRotationNotificationAsync(
        Guid subscriptionId,
        Guid keyId,
        DateTimeOffset notifiedAt,
        CancellationToken cancellationToken = default) =>
        WriteAsync(async db =>
        {
            WebhookSubscription subscription = await db.WebhookSubscriptions
                .Include(s => s.SigningKeys)
                .FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new EntityNotFoundException(typeof(WebhookSubscription), subscriptionId);

            bool stamped = subscription.StampRotationNotification(keyId, notifiedAt);
            if (!stamped)
            {
                throw new EntityNotFoundException(typeof(WebhookSigningKey), keyId);
            }
        }, cancellationToken);

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
