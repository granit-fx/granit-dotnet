using Granit.Events;

namespace Granit.Metering.Events;

/// <summary>Published when a tenant's usage reaches or exceeds 100% of their quota.</summary>
public sealed record QuotaExceededEto(
    Guid TenantId,
    Guid MeterDefinitionId,
    string MeterName,
    decimal CurrentUsage,
    decimal Limit) : IIntegrationEvent;
