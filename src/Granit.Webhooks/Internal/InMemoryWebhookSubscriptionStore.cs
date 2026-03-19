using System.Collections.Concurrent;
using System.Security.Cryptography;
using Granit.Core.Exceptions;
using Granit.Guids;
using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;

namespace Granit.Webhooks.Internal;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IWebhookSubscriptionReader"/> and
/// <see cref="IWebhookSubscriptionWriter"/>.
/// Suitable for development and unit tests. Does not persist across application restarts.
/// </summary>
internal sealed class InMemoryWebhookSubscriptionStore(
    IClock clock,
    IGuidGenerator guidGenerator,
    IWebhookSecretProtector secretProtector) : IWebhookSubscriptionReader, IWebhookSubscriptionWriter
{
    private readonly IClock _clock = clock;
    private readonly IGuidGenerator _guidGenerator = guidGenerator;
    private readonly IWebhookSecretProtector _secretProtector = secretProtector;
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
        string targetUrl,
        string eventType,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        string plainSecret = GenerateSigningSecret();
        string protectedSecret = await _secretProtector
            .ProtectAsync(plainSecret, cancellationToken)
            .ConfigureAwait(false);

        var subscription = WebhookSubscription.Create(
            _guidGenerator.Create(),
            targetUrl,
            eventType,
            protectedSecret,
            tenantId);

        _subscriptions[subscription.Id] = subscription;

        return new WebhookSubscriptionCreatedResult(subscription, plainSecret);
    }

    public Task UpdateTargetUrlAsync(Guid subscriptionId, string targetUrl, CancellationToken cancellationToken = default)
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
        if (!_subscriptions.TryGetValue(subscriptionId, out WebhookSubscription? subscription))
        {
            throw new EntityNotFoundException(typeof(WebhookSubscription), subscriptionId);
        }

        string plainSecret = GenerateSigningSecret();
        string protectedSecret = await _secretProtector
            .ProtectAsync(plainSecret, cancellationToken)
            .ConfigureAwait(false);

        subscription.RotateSecret(protectedSecret);
        return plainSecret;
    }

    /// <summary>
    /// Adds or replaces a subscription. Used in tests and dev scenarios.
    /// </summary>
    internal void Add(WebhookSubscription subscription) =>
        _subscriptions[subscription.Id] = subscription;

    private static string GenerateSigningSecret() =>
        $"whsec_{Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()}";
}
