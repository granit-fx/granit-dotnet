namespace Granit.Identity.Models;

/// <summary>
/// Represents a group from an external identity provider (e.g. Keycloak group).
/// </summary>
/// <param name="Id">Unique group identifier in the identity provider.</param>
/// <param name="Name">Group display name.</param>
/// <param name="Path">Full group path (e.g. <c>/parent/child</c>). <c>null</c> for root groups.</param>
/// <param name="SubGroups">Nested sub-groups. Empty list when the group has no children.</param>
public sealed record IdentityGroup(
    string Id,
    string Name,
    string? Path,
    IReadOnlyList<IdentityGroup> SubGroups);
