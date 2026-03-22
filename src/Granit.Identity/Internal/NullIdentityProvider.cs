using Granit.Identity.Models;

namespace Granit.Identity.Internal;

/// <summary>
/// Null-object implementation of <see cref="IIdentityProvider"/>.
/// Returns empty results for all queries and no-ops for write operations.
/// Registered by default when no provider package is installed.
/// </summary>
internal sealed class NullIdentityProvider : IIdentityProvider
{
    /// <inheritdoc/>
    public Task<IReadOnlyList<IIdentityUser>> GetUsersAsync(
        string? search = null, int? first = null, int? max = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IIdentityUser>>([]);

    /// <inheritdoc/>
    public Task<IIdentityUser?> GetUserAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IIdentityUser?>(null);

    /// <inheritdoc/>
    public Task SetUserEnabledAsync(
        string userId, bool enabled, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    public Task<IReadOnlyList<IdentitySession>> GetUserSessionsAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IdentitySession>>([]);

    /// <inheritdoc/>
    public Task<IReadOnlyList<IdentityDeviceActivity>> GetUserDeviceActivityAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IdentityDeviceActivity>>([]);

    /// <inheritdoc/>
    public Task<DateTimeOffset?> GetPasswordChangedAtAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<DateTimeOffset?>(null);

    /// <inheritdoc/>
    public Task UpdateUserAsync(
        string userId, IdentityUserUpdate update, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    public Task<IReadOnlyList<IdentityRole>> GetRolesAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IdentityRole>>([]);

    /// <inheritdoc/>
    public Task<IReadOnlyList<IIdentityUser>> GetRoleMembersAsync(
        string roleName, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IIdentityUser>>([]);

    /// <inheritdoc/>
    public Task<IReadOnlyList<IdentityRole>> GetUserRolesAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IdentityRole>>([]);

    /// <inheritdoc/>
    public Task AssignRoleAsync(
        string userId, string roleName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    public Task RemoveRoleAsync(
        string userId, string roleName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    public Task TerminateSessionAsync(
        string userId, string sessionId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    public Task TerminateAllSessionsAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    public Task SendPasswordResetEmailAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    public Task SetTemporaryPasswordAsync(
        string userId, string temporaryPassword, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    public Task<IIdentityUser> CreateUserAsync(
        IdentityUserCreate user, CancellationToken cancellationToken = default) =>
        Task.FromResult<IIdentityUser>(new NullIdentityUser(string.Empty, user.Username, user.Email,
            user.FirstName, user.LastName, user.Enabled));

    /// <inheritdoc/>
    public Task<IReadOnlyList<IdentityGroup>> GetGroupsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IdentityGroup>>([]);

    /// <inheritdoc/>
    public Task<IReadOnlyList<IdentityGroup>> GetUserGroupsAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IdentityGroup>>([]);

    /// <inheritdoc/>
    public Task AddUserToGroupAsync(
        string userId, string groupId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    public Task RemoveUserFromGroupAsync(
        string userId, string groupId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    public Task<bool> VerifyUserCredentialsAsync(
        string username, string password, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    /// <summary>
    /// Minimal <see cref="IIdentityUser"/> for the null provider.
    /// </summary>
    private sealed record NullIdentityUser(
        string UserId, string? Username, string? Email,
        string? FirstName, string? LastName, bool Enabled) : IIdentityUser
    {
        public IReadOnlyDictionary<string, string> ExtraProperties { get; } =
            System.Collections.ObjectModel.ReadOnlyDictionary<string, string>.Empty;
    }
}
