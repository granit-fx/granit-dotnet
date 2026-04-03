using Granit.Subscriptions.Domain;
using Granit.Workflow;

namespace Granit.Subscriptions.Definitions;

/// <summary>
/// Provides the two subscription lifecycle workflow definitions.
/// </summary>
/// <remarks>
/// Two definitions are needed because <see cref="WorkflowDefinition{TState}"/>
/// validates reachability from the initial state. Trial subscriptions start at
/// <see cref="SubscriptionStatus.Trial"/>; non-trial subscriptions start at
/// <see cref="SubscriptionStatus.Active"/>.
/// </remarks>
public static class SubscriptionWorkflows
{
    /// <summary>
    /// Workflow for subscriptions with a trial period.
    /// Initial state: <see cref="SubscriptionStatus.Trial"/>.
    /// </summary>
    public static WorkflowDefinition<SubscriptionStatus> WithTrial { get; } =
        WorkflowDefinition<SubscriptionStatus>.Create(builder => builder
            .InitialState(SubscriptionStatus.Trial)

            // Trial → Active (payment confirmed or trial converted)
            .Transition(SubscriptionStatus.Trial, SubscriptionStatus.Active, t => t
                .Named("Activate"))

            // Trial → Expired (trial ended without conversion)
            .Transition(SubscriptionStatus.Trial, SubscriptionStatus.Expired, t => t
                .Named("Expire Trial"))

            // Active → PastDue (payment failed)
            .Transition(SubscriptionStatus.Active, SubscriptionStatus.PastDue, t => t
                .Named("Payment Failed"))

            // Active → Cancelled (customer or admin cancels)
            .Transition(SubscriptionStatus.Active, SubscriptionStatus.Cancelled, t => t
                .Named("Cancel"))

            // PastDue → Active (payment recovered)
            .Transition(SubscriptionStatus.PastDue, SubscriptionStatus.Active, t => t
                .Named("Recover Payment"))

            // PastDue → Suspended (all retries exhausted)
            .Transition(SubscriptionStatus.PastDue, SubscriptionStatus.Suspended, t => t
                .Named("Suspend"))

            // Suspended → Cancelled (grace period expired)
            .Transition(SubscriptionStatus.Suspended, SubscriptionStatus.Cancelled, t => t
                .Named("Cancel After Suspension")));

    /// <summary>
    /// Workflow for subscriptions without a trial period (direct activation).
    /// Initial state: <see cref="SubscriptionStatus.Active"/>.
    /// </summary>
    public static WorkflowDefinition<SubscriptionStatus> Direct { get; } =
        WorkflowDefinition<SubscriptionStatus>.Create(builder => builder
            .InitialState(SubscriptionStatus.Active)

            // Active → PastDue (payment failed)
            .Transition(SubscriptionStatus.Active, SubscriptionStatus.PastDue, t => t
                .Named("Payment Failed"))

            // Active → Cancelled (customer or admin cancels)
            .Transition(SubscriptionStatus.Active, SubscriptionStatus.Cancelled, t => t
                .Named("Cancel"))

            // PastDue → Active (payment recovered)
            .Transition(SubscriptionStatus.PastDue, SubscriptionStatus.Active, t => t
                .Named("Recover Payment"))

            // PastDue → Suspended (all retries exhausted)
            .Transition(SubscriptionStatus.PastDue, SubscriptionStatus.Suspended, t => t
                .Named("Suspend"))

            // Suspended → Cancelled (grace period expired)
            .Transition(SubscriptionStatus.Suspended, SubscriptionStatus.Cancelled, t => t
                .Named("Cancel After Suspension")));
}
