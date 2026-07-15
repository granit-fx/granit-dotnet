using Granit.Identity.Models;

namespace Granit.Identity;

/// <summary>
/// Role management operations: list roles, query membership, assign and remove roles.
/// </summary>
public interface IIdentityRoleManager
{
    /// <summary>Lists every role defined in the identity provider.</summary>
    Task<IReadOnlyList<IdentityRole>> GetRolesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Lists the users that hold the specified role.</summary>
    Task<IReadOnlyList<IIdentityUser>> GetRoleMembersAsync(
        string roleName,
        CancellationToken cancellationToken = default);

    /// <summary>Lists the roles assigned to the specified user.</summary>
    Task<IReadOnlyList<IdentityRole>> GetUserRolesAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>Assigns the specified role to the specified user.</summary>
    Task AssignRoleAsync(
        string userId,
        string roleName,
        CancellationToken cancellationToken = default);

    /// <summary>Removes the specified role from the specified user.</summary>
    Task RemoveRoleAsync(
        string userId,
        string roleName,
        CancellationToken cancellationToken = default);
}
