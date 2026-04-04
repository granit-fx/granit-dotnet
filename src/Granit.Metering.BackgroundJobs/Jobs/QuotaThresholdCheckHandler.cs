using Granit.Metering.BackgroundJobs.Services;

namespace Granit.Metering.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="QuotaThresholdCheckJob"/>. Delegates to
/// <see cref="QuotaThresholdScanner"/> for quota detection and event publishing.
/// </summary>
public class QuotaThresholdCheckHandler
{
    public static async Task HandleAsync(
        QuotaThresholdCheckJob _,
        QuotaThresholdScanner scanner,
        CancellationToken cancellationToken) =>
        await scanner.ScanAsync(cancellationToken).ConfigureAwait(false);
}
