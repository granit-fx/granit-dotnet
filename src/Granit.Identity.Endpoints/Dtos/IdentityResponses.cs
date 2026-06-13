namespace Granit.Identity.Endpoints.Dtos;

/// <summary>User identity information.</summary>
public sealed record IdentityUserResponse(
    string UserId,
    string? Username,
    string? Email,
    string? FirstName,
    string? LastName,
    bool Enabled,
    IReadOnlyDictionary<string, string> Metadata);

/// <summary>Identity provider role.</summary>
public sealed record IdentityRoleResponse(
    string Id,
    string Name,
    string? Description);

/// <summary>Identity provider group.</summary>
public sealed record IdentityGroupResponse(
    string Id,
    string Name,
    string? Path,
    IReadOnlyList<IdentityGroupResponse> SubGroups);
