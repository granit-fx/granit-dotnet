using Granit.Events;

namespace Granit.Metering.Events;

/// <summary>Published when a tenant's usage reaches the configured threshold (default 80%).</summary>
public sealed record QuotaThresholdReachedEto(
    Guid TenantId,
    Guid MeterDefinitionId,
    string MeterName,
    decimal CurrentUsage,
    decimal Limit,
    decimal PercentUsed) : IIntegrationEvent;
