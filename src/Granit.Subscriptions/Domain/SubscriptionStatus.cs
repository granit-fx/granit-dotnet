namespace Granit.Subscriptions.Domain;

/// <summary>
/// Lifecycle status of a <see cref="Subscription"/>, managed by <c>Granit.Workflow</c> FSM.
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>Free trial period — limited time, full features per plan.</summary>
    Trial = 0,

    /// <summary>Active paid subscription — all plan features available.</summary>
    Active = 1,

    /// <summary>Payment failed — grace period before suspension.</summary>
    PastDue = 2,

    /// <summary>Suspended after extended non-payment — features restricted.</summary>
    Suspended = 3,

    /// <summary>Permanently cancelled — terminal state.</summary>
    Cancelled = 4,

    /// <summary>Trial expired without conversion — terminal state.</summary>
    Expired = 5,
}
