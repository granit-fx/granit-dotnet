using Granit.Subscriptions.Domain;

namespace Granit.Subscriptions.Internal.Internal;

/// <summary>
/// Self-hosted subscription provider. No-op executor — all lifecycle is handled
/// locally by Granit (BackgroundJobs, Billing events, Workflow FSM).
/// </summary>
internal sealed class InternalSubscriptionProvider : ISubscriptionProvider
{
    /// <inheritdoc />
    public string Name => "internal";

    /// <inheritdoc />
    public SubscriptionProviderCapabilities Capabilities => SubscriptionProviderCapabilities.None;

    /// <inheritdoc />
    public Task<(string ProviderName, string ExternalId)> CreateExternalAsync(
        Subscription subscription, Plan plan, CancellationToken cancellationToken = default) =>
        Task.FromResult(("internal", subscription.Id.ToString()));

    /// <inheritdoc />
    public Task CancelExternalAsync(
        SubscriptionExternalMapping mapping, bool atPeriodEnd, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task ChangePlanExternalAsync(
        SubscriptionExternalMapping mapping, Plan newPlan, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task UpdateSeatsExternalAsync(
        SubscriptionExternalMapping mapping, int quantity, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
