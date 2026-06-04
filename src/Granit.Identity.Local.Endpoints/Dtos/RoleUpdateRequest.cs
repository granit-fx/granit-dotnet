namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>Request to rename / update a local role's description. Side and tenant scope are immutable.</summary>
/// <param name="Name">New display name (max 256 chars).</param>
/// <param name="Description">New description (max 2048 chars, nullable to clear).</param>
public sealed record RoleUpdateRequest(string Name, string? Description = null);
