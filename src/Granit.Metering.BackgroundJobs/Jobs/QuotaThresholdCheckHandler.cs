using Granit.DataFiltering;
using Granit.Domain;
using Granit.Metering.Domain;
using Granit.Metering.Domain.ValueObjects;
using Granit.Metering.Dtos;
using Granit.Metering.Events;
using Granit.Metering.Options;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
        ICurrentTenant currentTenant,
        IDataFilter dataFilter,
        IMessageBus messageBus,
        IOptions<GranitMeteringOptions> options,
        IClock clock,
        ILogger<QuotaThresholdCheckJob> logger,
        CancellationToken cancellationToken)
    {
        Log.QuotaCheckStarted(logger, clock.Now);

        decimal threshold = options.Value.ThresholdPercentage;

        IReadOnlyList<MeterDefinition> definitions;
        using (dataFilter.Disable<IMultiTenant>())
        {
            definitions = await definitionReader.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        }
        foreach (MeterDefinition definition in definitions)
        {
            if (definition.TenantId is null)
            {
                continue;
            }

            using (currentTenant.Change(definition.TenantId.Value))
            {
                var meterId = MeterDefinitionId.Create(definition.Id);
                QuotaStatus status = await quotaChecker
                    .CheckAsync(definition.TenantId.Value, meterId, cancellationToken)
                    .ConfigureAwait(false);

                if (status.IsExceeded)
                {
                    await messageBus.PublishAsync(
                        new QuotaExceededEto(definition.TenantId.Value, definition.Id, status.MeterName, status.CurrentUsage, status.Limit!.Value))
                        .ConfigureAwait(false);
                }
                else if (status.Limit.HasValue && status.PercentUsed >= threshold)
                {
                    await messageBus.PublishAsync(
                        new QuotaThresholdReachedEto(definition.TenantId.Value, definition.Id, status.MeterName, status.CurrentUsage, status.Limit.Value, status.PercentUsed!.Value))
                        .ConfigureAwait(false);
                }
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
