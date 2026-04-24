namespace Granit.Metering.Endpoints.Dtos;

/// <summary>HTTP response for a successful event deprecation.</summary>
/// <param name="EventId">Id of the deprecated event.</param>
/// <param name="MeterDefinitionId">Owning meter.</param>
/// <param name="DeprecatedAt">Server-side UTC timestamp written on the event.</param>
/// <param name="AggregatesRebuilt">Number of <c>UsageAggregate</c> rows updated by the auto-recompute on the affected hourly bucket.</param>
public sealed record DeprecateEventResponse(
    Guid EventId,
    Guid MeterDefinitionId,
    DateTimeOffset DeprecatedAt,
    int AggregatesRebuilt);
