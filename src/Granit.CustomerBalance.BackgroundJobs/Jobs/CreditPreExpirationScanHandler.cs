namespace Granit.CustomerBalance.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="CreditPreExpirationScanJob"/>. Delegates to
/// <see cref="IPreExpirationScanService"/> for the daily warning emission.
/// </summary>
public class CreditPreExpirationScanHandler
{
    public static async Task HandleAsync(
        CreditPreExpirationScanJob _,
        IPreExpirationScanService scanService,
        CancellationToken cancellationToken) =>
        await scanService.ScanAsync(cancellationToken).ConfigureAwait(false);
}
