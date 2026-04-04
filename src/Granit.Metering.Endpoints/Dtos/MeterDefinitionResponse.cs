using Granit.Metering.Domain;

namespace Granit.Metering.Endpoints.Dtos;

/// <summary>
/// Read-only representation of a <see cref="MeterDefinition"/> for API responses.
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="Name">Meter display name.</param>
/// <param name="Unit">Unit of measure.</param>
/// <param name="Description">Optional description.</param>
/// <param name="AggregationType">How events are aggregated into rollups.</param>
/// <param name="IsActive">Whether this meter accepts new events.</param>
public sealed record MeterDefinitionResponse(
    Guid Id,
    string Name,
    string Unit,
    string? Description,
    AggregationType AggregationType,
    bool IsActive)
{
    internal static MeterDefinitionResponse FromEntity(MeterDefinition definition) => new(
        definition.Id,
        definition.Name,
        definition.Unit,
        definition.Description,
        definition.AggregationType,
        definition.IsActive);
}
