using Granit.Identity.Models;

namespace Granit.Identity.Local.Services;

/// <summary>
/// Abstracts group persistence for self-hosted identity providers.
/// </summary>
/// <remarks>
/// Decouples <c>AspNetIdentityProvider</c> from <c>OpenIddictDbContext</c> so that
/// any self-hosted provider (OpenIddict, Duende Identity Server) can implement
/// <see cref="Granit.Identity.IIdentityGroupManager"/> without a direct EF Core dependency
/// on a specific DbContext. The implementation (<c>OpenIddictGroupStore</c>) lives in
/// <c>Granit.OpenIddict.EntityFrameworkCore</c>.
/// </remarks>
public interface ILocalIdentityGroupStore
{
    /// <summary>Returns all groups visible to the current tenant.</summary>
    Task<IReadOnlyList<IdentityGroup>> GetGroupsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the groups that the specified user belongs to.</summary>
    Task<IReadOnlyList<IdentityGroup>> GetUserGroupsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>Adds a user to a group.</summary>
    Task AddUserToGroupAsync(string userId, string groupId, CancellationToken cancellationToken = default);

    /// <summary>Removes a user from a group.</summary>
    Task RemoveUserFromGroupAsync(string userId, string groupId, CancellationToken cancellationToken = default);
}
