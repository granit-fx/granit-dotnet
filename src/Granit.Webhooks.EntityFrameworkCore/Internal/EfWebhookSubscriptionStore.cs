using System.Security.Cryptography;
using Granit.Domain.ValueObjects;
using Granit.Exceptions;
using Granit.Guids;
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
/// <see cref="IWebhookSigningKeyWriter"/>. Dispatches every read and write through
/// <see cref="WebhooksContextResolver"/> so the same store serves both
/// <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Shared"/> (single
/// context) and <see cref="Granit.Persistence.MultiTenancy.DualScopeStorageMode.Segregated"/>
/// (host context + tenant context) deployments.
/// </summary>
internal sealed class EfWebhookSubscriptionStore(
    WebhooksContextResolver resolver,
    IGuidGenerator guidGenerator,
    IWebhookSecretProtector secretProtector,
    IClock clock,
    IOptions<WebhooksOptions> options) :
    IWebhookSubscriptionReader,
    IWebhookSubscriptionWriter,
    IWebhookSigningKeyReader,
    IWebhookSigningKeyWriter
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebhookSubscription>> GetActiveSubscriptionsAsync(
        string eventType,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IWebhooksDbContext> contexts = await resolver
            .OpenAllAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<WebhookSubscription> results = [];
            foreach (IWebhooksDbContext db in contexts)
            {
                List<WebhookSubscription> partial = await db.WebhookSubscriptions
                    .Include(s => s.SigningKeys)
                    .Where(s => s.Status == WebhookSubscriptionStatus.Active
                             && s.EventType == eventType
                             && (s.TenantId == null || s.TenantId == tenantId))
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                results.AddRange(partial);
            }

            return results;
        }
        finally
        {
            await DisposeAllAsync(contexts).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task<WebhookSubscription?> FindByIdAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IWebhooksDbContext> contexts = await resolver
            .OpenForUnknownScopeAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (IWebhooksDbContext db in contexts)
            {
                WebhookSubscription? hit = await db.WebhookSubscriptions
                    .Include(s => s.SigningKeys)
                    .FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken)
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

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebhookSubscription>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IWebhooksDbContext> contexts = await resolver
            .OpenAllAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<WebhookSubscription> results = [];
            foreach (IWebhooksDbContext db in contexts)
            {
                List<WebhookSubscription> partial = await db.WebhookSubscriptions
                    .Include(s => s.SigningKeys)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                results.AddRange(partial);
            }

            return results;
        }
        finally
        {
            await DisposeAllAsync(contexts).ConfigureAwait(false);
        }
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

        await using IWebhooksDbContext db = await resolver
            .OpenForScopeAsync(tenantId, cancellationToken).ConfigureAwait(false);
        db.WebhookSubscriptions.Add(subscription);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new WebhookSubscriptionCreatedResult(subscription, plainSecret);
    }

    /// <inheritdoc/>
    public Task UpdateTargetUrlAsync(
        Guid subscriptionId,
        HttpsUrl targetUrl,
        CancellationToken cancellationToken = default) =>
        WriteByIdAsync(subscriptionId, (_, subscription) => subscription.UpdateTargetUrl(targetUrl), cancellationToken);

    /// <inheritdoc/>
    public Task ActivateAsync(Guid subscriptionId, CancellationToken cancellationToken = default) =>
        WriteByIdAsync(subscriptionId, (_, subscription) => subscription.Activate(), cancellationToken);

    /// <inheritdoc/>
    public Task SuspendAsync(
        Guid subscriptionId,
        string suspendedBy,
        string reason,
        CancellationToken cancellationToken = default) =>
        WriteByIdAsync(
            subscriptionId,
            (_, subscription) => subscription.Suspend(clock.Now, suspendedBy, reason),
            cancellationToken);

    /// <inheritdoc/>
    public Task DeactivateAsync(
        Guid subscriptionId,
        string reason,
        CancellationToken cancellationToken = default) =>
        WriteByIdAsync(subscriptionId, (_, subscription) => subscription.Deactivate(reason), cancellationToken);

    /// <inheritdoc/>
    public Task DeleteAsync(Guid subscriptionId, CancellationToken cancellationToken = default) =>
        WriteByIdAsync(
            subscriptionId,
            (db, subscription) => db.WebhookSubscriptions.Remove(subscription),
            cancellationToken,
            includeSigningKeys: false);

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

        await WriteByIdAsync(
            subscriptionId,
            (_, subscription) => subscription.RotateSigningKey(newKeyId, protectedSecret, clock.Now, grace, newSigningSecretHint: hint),
            cancellationToken).ConfigureAwait(false);

        return new WebhookSigningKeyRotatedResult(newKeyId, plainSecret);
    }

    /// <inheritdoc/>
    public Task RevokeSigningKeyAsync(
        Guid subscriptionId,
        Guid keyId,
        CancellationToken cancellationToken = default) =>
        WriteByIdAsync(
            subscriptionId,
            (_, subscription) =>
            {
                bool revoked = subscription.RevokeSigningKey(keyId, clock.Now);
                if (!revoked)
                {
                    throw new EntityNotFoundException(typeof(WebhookSigningKey), keyId);
                }
            },
            cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebhookSigningKey>> GetForSubscriptionAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IWebhooksDbContext> contexts = await resolver
            .OpenForUnknownScopeAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<WebhookSigningKey> results = [];
            foreach (IWebhooksDbContext db in contexts)
            {
                List<WebhookSigningKey> partial = await db.WebhookSigningKeys
                    .Where(k => k.SubscriptionId == subscriptionId)
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                results.AddRange(partial);
            }

            return results;
        }
        finally
        {
            await DisposeAllAsync(contexts).ConfigureAwait(false);
        }
    }

    async Task<WebhookSigningKey?> IWebhookSigningKeyReader.FindByIdAsync(
        Guid keyId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<IWebhooksDbContext> contexts = await resolver
            .OpenForUnknownScopeAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (IWebhooksDbContext db in contexts)
            {
                WebhookSigningKey? hit = await db.WebhookSigningKeys
                    .FirstOrDefaultAsync(k => k.Id == keyId, cancellationToken)
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

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WebhookSigningKey>> GetExpiringSoonAsync(
        DateTimeOffset now,
        DateTimeOffset cutoff,
        DateTimeOffset notificationDedupeBefore,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IWebhooksDbContext> contexts = await resolver
            .OpenAllAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<WebhookSigningKey> results = [];
            foreach (IWebhooksDbContext db in contexts)
            {
                List<WebhookSigningKey> partial = await db.WebhookSigningKeys
                    .Where(k => (k.Status == WebhookSigningKeyStatus.Active
                              || k.Status == WebhookSigningKeyStatus.Retired)
                             && k.RevokedAt == null
                             && k.ExpiresAt != null
                             && k.ExpiresAt > now
                             && k.ExpiresAt <= cutoff
                             && (k.LastRotationNotificationAt == null
                                 || k.LastRotationNotificationAt < notificationDedupeBefore))
                    .ToListAsync(cancellationToken).ConfigureAwait(false);
                results.AddRange(partial);
            }

            return results;
        }
        finally
        {
            await DisposeAllAsync(contexts).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public Task StampRotationNotificationAsync(
        Guid subscriptionId,
        Guid keyId,
        DateTimeOffset notifiedAt,
        CancellationToken cancellationToken = default) =>
        WriteByIdAsync(
            subscriptionId,
            (_, subscription) =>
            {
                bool stamped = subscription.StampRotationNotification(keyId, notifiedAt);
                if (!stamped)
                {
                    throw new EntityNotFoundException(typeof(WebhookSigningKey), keyId);
                }
            },
            cancellationToken);

    /// <summary>
    /// Fetch-then-dispatch helper for any mutation keyed on a subscription <see cref="Guid"/>.
    /// Probes both host and tenant contexts under <c>Segregated</c>, applies the mutation
    /// in the owning context, then saves.
    /// </summary>
    private async Task WriteByIdAsync(
        Guid subscriptionId,
        Action<IWebhooksDbContext, WebhookSubscription> mutate,
        CancellationToken cancellationToken,
        bool includeSigningKeys = true)
    {
        IReadOnlyList<IWebhooksDbContext> contexts = await resolver
            .OpenForUnknownScopeAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (IWebhooksDbContext db in contexts)
            {
                IQueryable<WebhookSubscription> query = db.WebhookSubscriptions;
                if (includeSigningKeys)
                {
                    query = query.Include(s => s.SigningKeys);
                }

                WebhookSubscription? subscription = await query
                    .FirstOrDefaultAsync(s => s.Id == subscriptionId, cancellationToken)
                    .ConfigureAwait(false);

                if (subscription is null)
                {
                    continue;
                }

                mutate(db, subscription);
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            throw new EntityNotFoundException(typeof(WebhookSubscription), subscriptionId);
        }
        finally
        {
            await DisposeAllAsync(contexts).ConfigureAwait(false);
        }
    }

    private static async ValueTask DisposeAllAsync(IReadOnlyList<IWebhooksDbContext> contexts)
    {
        foreach (IWebhooksDbContext db in contexts)
        {
            await db.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static string GenerateSigningSecret() =>
        $"whsec_{Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()}";
}
