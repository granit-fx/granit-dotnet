namespace Granit.Metering.Endpoints.Dtos;

/// <summary>Request to update a meter definition.</summary>
/// <param name="Name">New display name.</param>
/// <param name="Unit">New unit of measure.</param>
/// <param name="Description">New description (null clears the value).</param>
public sealed record MeterDefinitionUpdateRequest(
    string Name,
    string Unit,
    string? Description = null);
