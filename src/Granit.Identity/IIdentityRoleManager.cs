using Granit.Identity.Models;

namespace Granit.Identity;

/// <summary>
/// Role management operations: list roles, query membership, assign and remove roles.
/// </summary>
public interface IIdentityRoleManager
{
    /// <inheritdoc cref="IIdentityProvider.GetRolesAsync"/>
    Task<IReadOnlyList<IdentityRole>> GetRolesAsync(
        CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IIdentityProvider.GetRoleMembersAsync"/>
    Task<IReadOnlyList<IIdentityUser>> GetRoleMembersAsync(
        string roleName,
        CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IIdentityProvider.GetUserRolesAsync"/>
    Task<IReadOnlyList<IdentityRole>> GetUserRolesAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IIdentityProvider.AssignRoleAsync"/>
    Task AssignRoleAsync(
        string userId,
        string roleName,
        CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IIdentityProvider.RemoveRoleAsync"/>
    Task RemoveRoleAsync(
        string userId,
        string roleName,
        CancellationToken cancellationToken = default);
}
