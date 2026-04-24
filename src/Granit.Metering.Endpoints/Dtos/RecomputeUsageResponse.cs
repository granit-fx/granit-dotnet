namespace Granit.Metering.Endpoints.Dtos;

/// <summary>HTTP response shape for an executed recompute.</summary>
public sealed record RecomputeUsageResponse(
    Guid MeterDefinitionId,
    DateTimeOffset WindowStart,
    DateTimeOffset WindowEnd,
    int EventsScanned,
    int AggregatesRebuilt,
    long DurationMilliseconds);
