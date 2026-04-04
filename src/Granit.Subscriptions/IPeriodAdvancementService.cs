namespace Granit.Subscriptions;

/// <summary>
/// Scans active subscriptions that have reached their period end and advances
/// the billing period based on the plan's default interval.
/// </summary>
public interface IPeriodAdvancementService
{
    /// <summary>Advances billing periods for all subscriptions past their current period end.</summary>
    Task AdvancePeriodsAsync(CancellationToken cancellationToken = default);
}
