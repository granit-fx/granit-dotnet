using Granit.Metering.Dtos;
using Granit.Metering.Events;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Granit.Metering.BackgroundJobs.Jobs;

/// <summary>
/// Handler for <see cref="QuotaThresholdCheckJob"/>. Checks current usage against quotas
/// and publishes <see cref="QuotaThresholdReachedEto"/> or <see cref="QuotaExceededEto"/>.
/// </summary>
internal static partial class QuotaThresholdCheckHandler
{
    public static async Task HandleAsync(
        QuotaThresholdCheckJob _,
        IMeterDefinitionReader definitionReader,
        IQuotaChecker quotaChecker,
        IMessageBus messageBus,
        IClock clock,
        ILogger<QuotaThresholdCheckJob> logger,
        CancellationToken cancellationToken)
    {
        Log.QuotaCheckStarted(logger, clock.Now);

        var definitions = await definitionReader.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        foreach (var definition in definitions)
        {
            if (definition.TenantId is null)
            {
                continue;
            }

            var status = await quotaChecker
                .CheckAsync(definition.TenantId.Value, definition.Id, cancellationToken)
                .ConfigureAwait(false);

            if (status.IsExceeded)
            {
                await messageBus.PublishAsync(
                    new QuotaExceededEto(definition.TenantId.Value, definition.Id.Value, status.MeterName, status.CurrentUsage, status.Limit!.Value),
                    cancellationToken).ConfigureAwait(false);
            }
            else if (status.Limit.HasValue && status.PercentUsed >= 80m)
            {
                await messageBus.PublishAsync(
                    new QuotaThresholdReachedEto(definition.TenantId.Value, definition.Id.Value, status.MeterName, status.CurrentUsage, status.Limit.Value, status.PercentUsed!.Value),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        Log.QuotaCheckCompleted(logger, clock.Now);
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Quota threshold check started at {Timestamp}")]
        public static partial void QuotaCheckStarted(ILogger logger, DateTimeOffset timestamp);

        [LoggerMessage(Level = LogLevel.Information, Message = "Quota threshold check completed at {Timestamp}")]
        public static partial void QuotaCheckCompleted(ILogger logger, DateTimeOffset timestamp);
    }
}
