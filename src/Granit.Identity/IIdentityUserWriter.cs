using Granit.Identity.Models;

namespace Granit.Identity;

/// <summary>
/// Write operations on identity users (enable/disable, update profile, create).
/// </summary>
public interface IIdentityUserWriter
{
    /// <inheritdoc cref="IIdentityProvider.SetUserEnabledAsync"/>
    Task SetUserEnabledAsync(
        string userId,
        bool enabled,
        CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IIdentityProvider.UpdateUserAsync"/>
    Task UpdateUserAsync(
        string userId,
        IdentityUserUpdate update,
        CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IIdentityProvider.CreateUserAsync"/>
    Task<IIdentityUser> CreateUserAsync(
        IdentityUserCreate user,
        CancellationToken cancellationToken = default);
}
