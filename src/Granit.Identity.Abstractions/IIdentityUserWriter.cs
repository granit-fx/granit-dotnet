using Granit.Identity.Models;

namespace Granit.Identity;

/// <summary>
/// Write operations on identity users (enable/disable, update profile, create).
/// </summary>
public interface IIdentityUserWriter
{
    /// <summary>Enables or disables the specified user account.</summary>
    Task SetUserEnabledAsync(
        string userId,
        bool enabled,
        CancellationToken cancellationToken = default);

    /// <summary>Applies the supplied profile changes to the specified user.</summary>
    Task UpdateUserAsync(
        string userId,
        IdentityUserUpdate update,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a new identity user and returns the persisted record.</summary>
    Task<IIdentityUser> CreateUserAsync(
        IdentityUserCreate user,
        CancellationToken cancellationToken = default);
}
