namespace Granit.OpenIddict.Endpoints.Dtos;

/// <summary>
/// Response DTO for role administration endpoints.
/// </summary>
/// <param name="Name">The role name.</param>
/// <param name="Description">Human-readable description.</param>
public sealed record AdminRoleResponse(
    string Name,
    string? Description);
