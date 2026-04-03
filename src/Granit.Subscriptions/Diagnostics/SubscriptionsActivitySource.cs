using System.Diagnostics;

namespace Granit.Subscriptions.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Subscriptions distributed tracing.
/// </summary>
internal static class SubscriptionsActivitySource
{
    internal const string Name = "Granit.Subscriptions";

    internal static readonly ActivitySource Source = new(Name);

    internal const string CreateSubscription = "subscriptions.create";
    internal const string ActivateSubscription = "subscriptions.activate";
    internal const string CancelSubscription = "subscriptions.cancel";
    internal const string ChangePlan = "subscriptions.change_plan";
    internal const string AdvancePeriod = "subscriptions.advance_period";
}
