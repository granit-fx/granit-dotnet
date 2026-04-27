using System.Collections.Concurrent;
using System.Security.Cryptography;
using Granit.Domain.ValueObjects;
using Granit.Exceptions;
using Granit.Guids;
using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Granit.Webhooks.Options;
using Microsoft.Extensions.Options;

namespace Granit.Webhooks.Internal;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IWebhookSubscriptionReader"/>,
/// <see cref="IWebhookSubscriptionWriter"/>, <see cref="IWebhookSigningKeyReader"/>, and
/// <see cref="IWebhookSigningKeyWriter"/>.
/// Suitable for development and unit tests. Does not persist across application restarts.
/// </summary>
internal sealed class InMemoryWebhookSubscriptionStore(
    IClock clock,
    IGuidGenerator guidGenerator,
    IWebhookSecretProtector secretProtector,
    IOptions<WebhooksOptions> options)
    : IWebhookSubscriptionReader,
      IWebhookSubscriptionWriter,
      IWebhookSigningKeyReader,
      IWebhookSigningKeyWriter
{
    private readonly IClock _clock = clock;
    private readonly IGuidGenerator _guidGenerator = guidGenerator;
    private readonly IWebhookSecretProtector _secretProtector = secretProtector;
    private readonly IOptions<WebhooksOptions> _options = options;
    private readonly ConcurrentDictionary<Guid, WebhookSubscription> _subscriptions = new();

    public Task<IReadOnlyList<WebhookSubscription>> GetActiveSubscriptionsAsync(
        string eventType,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WebhookSubscription> results = _subscriptions.Values
            .Where(s => s.Status == WebhookSubscriptionStatus.Active
                     && s.EventType == eventType
                     && (s.TenantId == null || s.TenantId == tenantId))
            .ToList();

        return Task.FromResult(results);
    }

    public Task<WebhookSubscription?> FindByIdAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        _subscriptions.TryGetValue(subscriptionId, out WebhookSubscription? subscription);
        return Task.FromResult(subscription);
    }

    public Task<IReadOnlyList<WebhookSubscription>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WebhookSubscription> results = _subscriptions.Values.ToList();
        return Task.FromResult(results);
    }

    public async Task<WebhookSubscriptionCreatedResult> CreateAsync(
        HttpsUrl targetUrl,
        string eventType,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        string plainSecret = GenerateSigningSecret();
        string protectedSecret = await _secretProtector
            .ProtectAsync(plainSecret, cancellationToken)
            .ConfigureAwait(false);

        var subscription = WebhookSubscription.CreateWithSigningKey(
            _guidGenerator.Create(),
            targetUrl,
            eventType,
            _guidGenerator.Create(),
            protectedSecret,
            _clock.Now,
            tenantId);

        _subscriptions[subscription.Id] = subscription;

        return new WebhookSubscriptionCreatedResult(subscription, plainSecret);
    }

    public Task UpdateTargetUrlAsync(Guid subscriptionId, HttpsUrl targetUrl, CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryGetValue(subscriptionId, out WebhookSubscription? subscription))
        {
            throw new EntityNotFoundException(typeof(WebhookSubscription), subscriptionId);
        }

        subscription.UpdateTargetUrl(targetUrl);
        return Task.CompletedTask;
    }

    public Task ActivateAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryGetValue(subscriptionId, out WebhookSubscription? subscription))
        {
            throw new EntityNotFoundException(typeof(WebhookSubscription), subscriptionId);
        }

        if (subscription.Status != WebhookSubscriptionStatus.Suspended)
        {
            throw new ConflictException(
                "Webhooks:InvalidStateTransition",
                $"Cannot activate a subscription with status '{subscription.Status}'. Only 'Suspended' subscriptions can be activated.");
        }

        subscription.Activate();
        return Task.CompletedTask;
    }

    public Task SuspendAsync(Guid subscriptionId, string suspendedBy, string reason, CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryGetValue(subscriptionId, out WebhookSubscription? subscription))
        {
            throw new EntityNotFoundException(typeof(WebhookSubscription), subscriptionId);
        }

        if (subscription.Status != WebhookSubscriptionStatus.Active)
        {
            throw new ConflictException(
                "Webhooks:InvalidStateTransition",
                $"Cannot suspend a subscription with status '{subscription.Status}'. Only 'Active' subscriptions can be suspended.");
        }

        subscription.Suspend(_clock.Now, suspendedBy, reason);
        return Task.CompletedTask;
    }

    public Task DeactivateAsync(Guid subscriptionId, string reason, CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryGetValue(subscriptionId, out WebhookSubscription? subscription))
        {
            throw new EntityNotFoundException(typeof(WebhookSubscription), subscriptionId);
        }

        if (subscription.Status == WebhookSubscriptionStatus.Deactivated)
        {
            throw new ConflictException(
                "Webhooks:AlreadyDeactivated",
                "Subscription is already deactivated.");
        }

        subscription.Deactivate(reason);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryRemove(subscriptionId, out _))
        {
            throw new EntityNotFoundException(typeof(WebhookSubscription), subscriptionId);
        }

        return Task.CompletedTask;
    }

    public async Task<string> RotateSecretAsync(Guid subscriptionId, CancellationToken cancellationToken = default)
    {
        WebhookSigningKeyRotatedResult result = await ((IWebhookSigningKeyWriter)this)
            .RotateSigningKeyAsync(subscriptionId, retiredKeyGracePeriod: null, cancellationToken)
            .ConfigureAwait(false);

        return result.PlainSecret;
    }

    public async Task<WebhookSigningKeyRotatedResult> RotateSigningKeyAsync(
        Guid subscriptionId,
        TimeSpan? retiredKeyGracePeriod = null,
        CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryGetValue(subscriptionId, out WebhookSubscription? subscription))
        {
            throw new EntityNotFoundException(typeof(WebhookSubscription), subscriptionId);
        }

        string plainSecret = GenerateSigningSecret();
        string protectedSecret = await _secretProtector
            .ProtectAsync(plainSecret, cancellationToken)
            .ConfigureAwait(false);

        TimeSpan grace = retiredKeyGracePeriod ?? _options.Value.RetiredKeyGracePeriod;
        Guid newKeyId = _guidGenerator.Create();

        WebhookSigningKey newKey = subscription.RotateSigningKey(
            newKeyId,
            protectedSecret,
            _clock.Now,
            grace);

        return new WebhookSigningKeyRotatedResult(newKey.Id, plainSecret);
    }

    public Task RevokeSigningKeyAsync(
        Guid subscriptionId,
        Guid keyId,
        CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryGetValue(subscriptionId, out WebhookSubscription? subscription))
        {
            throw new EntityNotFoundException(typeof(WebhookSubscription), subscriptionId);
        }

        bool revoked = subscription.RevokeSigningKey(keyId, _clock.Now);
        if (!revoked)
        {
            throw new EntityNotFoundException(typeof(WebhookSigningKey), keyId);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WebhookSigningKey>> GetForSubscriptionAsync(
        Guid subscriptionId,
        CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryGetValue(subscriptionId, out WebhookSubscription? subscription))
        {
            return Task.FromResult<IReadOnlyList<WebhookSigningKey>>([]);
        }

        IReadOnlyList<WebhookSigningKey> keys = subscription.SigningKeys.ToList();
        return Task.FromResult(keys);
    }

    Task<WebhookSigningKey?> IWebhookSigningKeyReader.FindByIdAsync(Guid keyId, CancellationToken cancellationToken)
    {
        foreach (WebhookSubscription sub in _subscriptions.Values)
        {
            foreach (WebhookSigningKey key in sub.SigningKeys)
            {
                if (key.Id == keyId)
                {
                    return Task.FromResult<WebhookSigningKey?>(key);
                }
            }
        }

        return Task.FromResult<WebhookSigningKey?>(null);
    }

    public Task<IReadOnlyList<WebhookSigningKey>> GetExpiringSoonAsync(
        DateTimeOffset now,
        DateTimeOffset cutoff,
        DateTimeOffset notificationDedupeBefore,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<WebhookSigningKey> results = _subscriptions.Values
            .SelectMany(s => s.SigningKeys)
            .Where(k => k.Status is WebhookSigningKeyStatus.Active or WebhookSigningKeyStatus.Retired
                     && k.RevokedAt is null
                     && k.ExpiresAt is { } expiresAt
                     && expiresAt > now
                     && expiresAt <= cutoff
                     && (k.LastRotationNotificationAt is null
                         || k.LastRotationNotificationAt < notificationDedupeBefore))
            .ToList();

        return Task.FromResult(results);
    }

    public Task StampRotationNotificationAsync(
        Guid subscriptionId,
        Guid keyId,
        DateTimeOffset notifiedAt,
        CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryGetValue(subscriptionId, out WebhookSubscription? subscription))
        {
            throw new EntityNotFoundException(typeof(WebhookSubscription), subscriptionId);
        }

        bool stamped = subscription.StampRotationNotification(keyId, notifiedAt);
        if (!stamped)
        {
            throw new EntityNotFoundException(typeof(WebhookSigningKey), keyId);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds or replaces a subscription. Used in tests and dev scenarios.
    /// </summary>
    internal void Add(WebhookSubscription subscription) =>
        _subscriptions[subscription.Id] = subscription;

    private static string GenerateSigningSecret() =>
        $"whsec_{Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()}";
}
