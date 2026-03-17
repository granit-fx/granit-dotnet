namespace Granit.Features.Endpoints.Dtos;

/// <summary>
/// Request to set a tenant-level feature override.
/// </summary>
/// <param name="Value">The override value.</param>
public sealed record SetFeatureOverrideRequest(string Value);
