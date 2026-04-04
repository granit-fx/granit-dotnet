using Granit.Metering.BackgroundJobs.Services;

namespace Granit.Metering.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="MeteringAggregationJob"/>. Delegates to
/// <see cref="MeteringAggregationScanner"/> for aggregation orchestration.
/// </summary>
public class MeteringAggregationHandler
{
    public static async Task HandleAsync(
        MeteringAggregationJob _,
        MeteringAggregationScanner scanner,
        CancellationToken cancellationToken) =>
        await scanner.RunAsync(cancellationToken).ConfigureAwait(false);
}
