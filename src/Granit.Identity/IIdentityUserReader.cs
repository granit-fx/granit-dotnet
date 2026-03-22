namespace Granit.Identity;

/// <summary>
/// Read-only access to identity users from an identity provider (local or federated).
/// </summary>
public interface IIdentityUserReader
{
    /// <inheritdoc cref="IIdentityProvider.GetUsersAsync"/>
    Task<IReadOnlyList<IIdentityUser>> GetUsersAsync(
        string? search = null,
        int? first = null,
        int? max = null,
        CancellationToken cancellationToken = default);

    /// <inheritdoc cref="IIdentityProvider.GetUserAsync"/>
    Task<IIdentityUser?> GetUserAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
