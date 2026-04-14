using Granit.Metering;
using Granit.Modularity;
using Granit.Scheduling;
using Granit.Subscriptions.Extensions;
using Granit.Timing;
using Granit.Workflow;

namespace Granit.Subscriptions;

/// <summary>
/// Granit module for subscription management (plans, subscriptions, seats, entitlements).
/// </summary>
/// <remarks>
/// Provides the domain model, FSM definitions, provider abstraction, and CQRS interfaces.
/// Add <c>Granit.Subscriptions.Internal</c> or <c>Granit.Subscriptions.Stripe</c> for
/// a concrete <see cref="ISubscriptionProvider"/> implementation.
/// </remarks>
[DependsOn(
    typeof(GranitMeteringModule),
    typeof(GranitSchedulingModule),
    typeof(GranitTimingModule),
    typeof(GranitWorkflowModule))]
public sealed class GranitSubscriptionsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitSubscriptions();
}
