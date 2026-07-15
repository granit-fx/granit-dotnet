using Granit.Identity.Models;

namespace Granit.Identity;

/// <summary>
/// Group management operations: list groups, query membership, add and remove users.
/// </summary>
public interface IIdentityGroupManager
{
    /// <summary>Lists every group defined in the identity provider.</summary>
    Task<IReadOnlyList<IdentityGroup>> GetGroupsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Lists the groups the specified user belongs to.</summary>
    Task<IReadOnlyList<IdentityGroup>> GetUserGroupsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>Adds the specified user to the specified group.</summary>
    Task AddUserToGroupAsync(
        string userId,
        string groupId,
        CancellationToken cancellationToken = default);

    /// <summary>Removes the specified user from the specified group.</summary>
    Task RemoveUserFromGroupAsync(
        string userId,
        string groupId,
        CancellationToken cancellationToken = default);
}
