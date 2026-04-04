namespace Granit.Subscriptions.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="PeriodEndScanJob"/>. Delegates to
/// <see cref="IPeriodAdvancementService"/> for billing period advancement.
/// </summary>
internal static class PeriodEndScanHandler
{
    public static async Task HandleAsync(
        PeriodEndScanJob _,
        IPeriodAdvancementService periodAdvancementService,
        CancellationToken cancellationToken) =>
        await periodAdvancementService.AdvancePeriodsAsync(cancellationToken).ConfigureAwait(false);
}
