using Granit.Subscriptions.BackgroundJobs.Services;

namespace Granit.Subscriptions.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="TrialExpirationScanJob"/>. Delegates to
/// <see cref="TrialExpirationScanner"/> for trial detection and processing.
/// </summary>
public class TrialExpirationScanHandler
{
    public static async Task HandleAsync(
        TrialExpirationScanJob _,
        TrialExpirationScanner scanner,
        CancellationToken cancellationToken) =>
        await scanner.ScanAsync(cancellationToken).ConfigureAwait(false);
}
