using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Metering.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="MeteringAggregationJob"/>. Delegates to <see cref="IAggregationRunner"/>
/// which reads events past the watermark, computes rollups, and advances the watermark atomically.
/// </summary>
internal static partial class MeteringAggregationHandler
{
    public static async Task HandleAsync(
        MeteringAggregationJob _,
        IAggregationRunner aggregationRunner,
        IClock clock,
        ILogger<MeteringAggregationJob> logger,
        CancellationToken cancellationToken)
    {
        Log.AggregationStarted(logger, clock.Now);

        await aggregationRunner.RunAsync(cancellationToken).ConfigureAwait(false);

        Log.AggregationCompleted(logger, clock.Now);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Metering aggregation started at {Timestamp}")]
        public static partial void AggregationStarted(ILogger logger, DateTimeOffset timestamp);

        [LoggerMessage(Level = LogLevel.Information, Message = "Metering aggregation completed at {Timestamp}")]
        public static partial void AggregationCompleted(ILogger logger, DateTimeOffset timestamp);
    }
}
