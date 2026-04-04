namespace Granit.Subscriptions.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="CancelAtPeriodEndScanJob"/>. Delegates to
/// <see cref="ICancelAtPeriodEndService"/> for subscription cancellation.
/// </summary>
internal static class CancelAtPeriodEndScanHandler
{
    public static async Task HandleAsync(
        CancelAtPeriodEndScanJob _,
        ICancelAtPeriodEndService cancelAtPeriodEndService,
        CancellationToken cancellationToken) =>
        await cancelAtPeriodEndService.CancelDueSubscriptionsAsync(cancellationToken).ConfigureAwait(false);
}
