using Granit.Identity.Models;
using Granit.Tests.Shared;

namespace Granit.Identity.Tests;

/// <summary>
/// Minimal <see cref="IIdentityProvider"/> implementation used in DI registration tests.
/// </summary>
internal sealed class FakeIdentityProvider : IIdentityProvider
{
    public Task<IReadOnlyList<IIdentityUser>> GetUsersAsync(
        string? search = null, int? first = null, int? max = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IIdentityUser>>([]);

    public Task<IIdentityUser?> GetUserAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IIdentityUser?>(null);

    public Task SetUserEnabledAsync(
        string userId, bool enabled, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<IdentitySession>> GetUserSessionsAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IdentitySession>>([]);

    public Task<IReadOnlyList<IdentityDeviceActivity>> GetUserDeviceActivityAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IdentityDeviceActivity>>([]);

    public Task<DateTimeOffset?> GetPasswordChangedAtAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<DateTimeOffset?>(null);

    public Task UpdateUserAsync(
        string userId, IdentityUserUpdate update, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<IdentityRole>> GetRolesAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IdentityRole>>([]);

    public Task<IReadOnlyList<IIdentityUser>> GetRoleMembersAsync(
        string roleName, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IIdentityUser>>([]);

    public Task<IReadOnlyList<IdentityRole>> GetUserRolesAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IdentityRole>>([]);

    public Task AssignRoleAsync(
        string userId, string roleName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RemoveRoleAsync(
        string userId, string roleName, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task TerminateSessionAsync(
        string userId, string sessionId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task TerminateAllSessionsAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SendPasswordResetEmailAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task SetTemporaryPasswordAsync(
        string userId, string temporaryPassword, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IIdentityUser> CreateUserAsync(
        IdentityUserCreate user, CancellationToken cancellationToken = default) =>
        Task.FromResult<IIdentityUser>(new FakeIdentityUser(string.Empty, user.Username, user.Email,
            user.FirstName, user.LastName, user.Enabled));

    public Task<IReadOnlyList<IdentityGroup>> GetGroupsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IdentityGroup>>([]);

    public Task<IReadOnlyList<IdentityGroup>> GetUserGroupsAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IdentityGroup>>([]);

    public Task AddUserToGroupAsync(
        string userId, string groupId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task RemoveUserFromGroupAsync(
        string userId, string groupId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<bool> VerifyUserCredentialsAsync(
        string username, string password, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
