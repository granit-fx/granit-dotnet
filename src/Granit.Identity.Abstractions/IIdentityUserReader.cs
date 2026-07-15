namespace Granit.Identity;

/// <summary>
/// Read-only access to identity users from an identity provider (local or federated).
/// </summary>
public interface IIdentityUserReader
{
    /// <summary>Lists users from the identity provider, optionally filtered by a search term.</summary>
    Task<IReadOnlyList<IIdentityUser>> GetUsersAsync(
        string? search = null,
        int? first = null,
        int? max = null,
        CancellationToken cancellationToken = default);

    /// <summary>Returns a single user by ID, or <c>null</c> if not found.</summary>
    Task<IIdentityUser?> GetUserAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
