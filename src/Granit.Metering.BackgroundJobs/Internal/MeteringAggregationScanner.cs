using Granit.Timing;
using Microsoft.Extensions.Logging;

namespace Granit.Metering.BackgroundJobs.Internal;

/// <summary>
/// Orchestrates metering aggregation by delegating to <see cref="IAggregationRunner"/>
/// with structured logging around the operation.
/// </summary>
internal sealed partial class MeteringAggregationScanner(
    IAggregationRunner aggregationRunner,
    IClock clock,
    ILogger<MeteringAggregationScanner> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
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
