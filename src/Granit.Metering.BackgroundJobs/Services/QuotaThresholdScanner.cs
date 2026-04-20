using Granit.DataFiltering;
using Granit.Domain;
using Granit.Events;
using Granit.Metering.Domain;
using Granit.Metering.Domain.ValueObjects;
using Granit.Metering.Dtos;
using Granit.Metering.Events;
using Granit.Metering.Options;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Metering.BackgroundJobs.Services;

/// <summary>
/// Scans all active meter definitions across tenants, checks current usage
/// against quotas, and publishes threshold or exceeded integration events.
/// </summary>
public sealed partial class QuotaThresholdScanner(
    IMeterDefinitionReader definitionReader,
    IQuotaChecker quotaChecker,
    ICurrentTenant currentTenant,
    IDataFilter dataFilter,
    IDistributedEventBus distributedEventBus,
    IOptions<GranitMeteringOptions> options,
    IClock clock,
    ILogger<QuotaThresholdScanner> logger)
{
    public async Task ScanAsync(CancellationToken cancellationToken)
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
                    await distributedEventBus.PublishAsync(
                        new QuotaExceededEto(definition.TenantId.Value, definition.Id, status.MeterName, status.CurrentUsage, status.Limit!.Value),
                        cancellationToken)
                        .ConfigureAwait(false);
                }
                else if (status.Limit.HasValue && status.PercentUsed >= threshold)
                {
                    await distributedEventBus.PublishAsync(
                        new QuotaThresholdReachedEto(definition.TenantId.Value, definition.Id, status.MeterName, status.CurrentUsage, status.Limit.Value, status.PercentUsed!.Value),
                        cancellationToken)
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
