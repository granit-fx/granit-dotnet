using Granit.Metering.BackgroundJobs.Internal;

namespace Granit.Metering.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="MeteringAggregationJob"/>. Delegates to
/// <see cref="MeteringAggregationScanner"/> for aggregation orchestration.
/// </summary>
internal static class MeteringAggregationHandler
{
    public static async Task HandleAsync(
        MeteringAggregationJob _,
        MeteringAggregationScanner scanner,
        CancellationToken cancellationToken) =>
        await scanner.RunAsync(cancellationToken).ConfigureAwait(false);
}
