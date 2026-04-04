using Granit.Metering.Domain;

namespace Granit.Metering.Endpoints.Dtos;

/// <summary>Request to create a new meter definition.</summary>
/// <param name="Name">Meter display name (e.g., "API Calls").</param>
/// <param name="Unit">Unit of measure (e.g., "requests", "GB").</param>
/// <param name="AggregationType">How events are aggregated into rollups.</param>
/// <param name="Description">Optional description.</param>
public sealed record MeterDefinitionCreateRequest(
    string Name,
    string Unit,
    AggregationType AggregationType,
    string? Description = null);
