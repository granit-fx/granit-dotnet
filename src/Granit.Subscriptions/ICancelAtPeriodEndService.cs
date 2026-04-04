namespace Granit.Subscriptions;

/// <summary>
/// Scans subscriptions flagged for cancellation at period end and cancels them.
/// </summary>
public interface ICancelAtPeriodEndService
{
    /// <summary>Cancels all subscriptions that have reached their period end and are flagged for cancellation.</summary>
    Task CancelDueSubscriptionsAsync(CancellationToken cancellationToken = default);
}
