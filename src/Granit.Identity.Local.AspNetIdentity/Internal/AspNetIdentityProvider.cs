using Granit.Identity;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Services;
using Granit.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GranitIdentityGroup = Granit.Identity.Models.IdentityGroup;
using GranitIdentityRole = Granit.Identity.Models.IdentityRole;

namespace Granit.Identity.Local.AspNetIdentity.Internal;

/// <summary>
/// <see cref="IIdentityProvider"/> implementation backed by ASP.NET Core Identity.
/// </summary>
/// <remarks>
/// Implements all 7 sub-interfaces of <see cref="IIdentityProvider"/>:
/// <see cref="IIdentityUserReader"/>, <see cref="IIdentityUserWriter"/>,
/// <see cref="IIdentityRoleManager"/>, <see cref="IIdentityGroupManager"/>,
/// <see cref="IIdentitySessionManager"/>, <see cref="IIdentityPasswordManager"/>,
/// <see cref="IIdentityCredentialVerifier"/>.
/// </remarks>
internal sealed partial class AspNetIdentityProvider(
    UserManager<GranitUser> _userManager,
    RoleManager<GranitRole> _roleManager,
    ILocalIdentityGroupStore _groupStore,
    ILogger<AspNetIdentityProvider> _logger) : IIdentityProvider
{
    // ──── IIdentityUserReader ────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IIdentityUser>> GetUsersAsync(
        string? search = null, int? first = null, int? max = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<GranitUser> query = _userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                (u.UserName != null && u.UserName.Contains(search)) ||
                (u.Email != null && u.Email.Contains(search)) ||
                (u.FirstName != null && u.FirstName.Contains(search)) ||
                (u.LastName != null && u.LastName.Contains(search)));
        }

        query = query.OrderBy(u => u.UserName);

        if (first.HasValue)
        {
            query = query.Skip(first.Value);
        }

        if (max.HasValue)
        {
            query = query.Take(max.Value);
        }

        List<GranitUser> users = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        return users.Cast<IIdentityUser>().ToList();
    }

    /// <inheritdoc/>
    public async Task<IIdentityUser?> GetUserAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        GranitUser? user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        return user;
    }

    // ──── IIdentityUserWriter ────

    /// <inheritdoc/>
    public async Task<IIdentityUser> CreateUserAsync(
        IdentityUserCreate user, CancellationToken cancellationToken = default)
    {
        GranitUser entity = new()
        {
            UserName = user.Username,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
        };

        IdentityResult result = string.IsNullOrEmpty(user.TemporaryPassword)
            ? await _userManager.CreateAsync(entity).ConfigureAwait(false)
            : await _userManager.CreateAsync(entity, user.TemporaryPassword).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            string errors = string.Join(", ", result.Errors.Select(e => e.Description));
            Log.IdentityOperationFailed(_logger, "User creation", errors);
            throw new InvalidOperationException($"User creation failed: {errors}");
        }

        if (!user.Enabled)
        {
            await _userManager.SetLockoutEndDateAsync(entity, DateTimeOffset.MaxValue).ConfigureAwait(false);
        }

        return entity;
    }

    /// <inheritdoc/>
    public async Task SetUserEnabledAsync(
        string userId, bool enabled, CancellationToken cancellationToken = default)
    {
        GranitUser user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        if (enabled)
        {
            await _userManager.SetLockoutEndDateAsync(user, null).ConfigureAwait(false);
        }
        else
        {
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async Task UpdateUserAsync(
        string userId, IdentityUserUpdate update, CancellationToken cancellationToken = default)
    {
        GranitUser user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        if (update.FirstName is not null)
        {
            user.FirstName = update.FirstName;
        }

        if (update.LastName is not null)
        {
            user.LastName = update.LastName;
        }

        if (update.Email is not null)
        {
            await _userManager.SetEmailAsync(user, update.Email).ConfigureAwait(false);
        }

        IdentityResult result = await _userManager.UpdateAsync(user).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            string errors = string.Join(", ", result.Errors.Select(e => e.Description));
            Log.IdentityOperationFailed(_logger, "User update", errors);
            throw new InvalidOperationException($"User update failed: {errors}");
        }
    }

    // ──── IIdentityRoleManager ────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<GranitIdentityRole>> GetRolesAsync(
        CancellationToken cancellationToken = default)
    {
        List<GranitRole> roles = await _roleManager.Roles.AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return roles.Select(r => new GranitIdentityRole(r.Id.ToString(), r.Name ?? string.Empty, r.Description)).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IIdentityUser>> GetRoleMembersAsync(
        string roleName, CancellationToken cancellationToken = default)
    {
        IList<GranitUser> users = await _userManager.GetUsersInRoleAsync(roleName).ConfigureAwait(false);
        return users.Cast<IIdentityUser>().ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<GranitIdentityRole>> GetUserRolesAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        GranitUser? user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false);
        if (user is null)
        {
            return [];
        }

        IList<string> roleNames = await _userManager.GetRolesAsync(user).ConfigureAwait(false);
        return roleNames.Select(name => new GranitIdentityRole(string.Empty, name, null)).ToList();
    }

    /// <inheritdoc/>
    public async Task AssignRoleAsync(
        string userId, string roleName, CancellationToken cancellationToken = default)
    {
        GranitUser user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        IdentityResult result = await _userManager.AddToRoleAsync(user, roleName).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            string errors = string.Join(", ", result.Errors.Select(e => e.Description));
            Log.IdentityOperationFailed(_logger, "Role assignment", errors);
            throw new InvalidOperationException($"Role assignment failed: {errors}");
        }
    }

    /// <inheritdoc/>
    public async Task RemoveRoleAsync(
        string userId, string roleName, CancellationToken cancellationToken = default)
    {
        GranitUser user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        IdentityResult result = await _userManager.RemoveFromRoleAsync(user, roleName).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            string errors = string.Join(", ", result.Errors.Select(e => e.Description));
            Log.IdentityOperationFailed(_logger, "Role removal", errors);
            throw new InvalidOperationException($"Role removal failed: {errors}");
        }
    }

    // ──── IIdentityGroupManager ────

    /// <inheritdoc/>
    public Task<IReadOnlyList<GranitIdentityGroup>> GetGroupsAsync(
        CancellationToken cancellationToken = default) =>
        _groupStore.GetGroupsAsync(cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<GranitIdentityGroup>> GetUserGroupsAsync(
        string userId, CancellationToken cancellationToken = default) =>
        _groupStore.GetUserGroupsAsync(userId, cancellationToken);

    /// <inheritdoc/>
    public Task AddUserToGroupAsync(
        string userId, string groupId, CancellationToken cancellationToken = default) =>
        _groupStore.AddUserToGroupAsync(userId, groupId, cancellationToken);

    /// <inheritdoc/>
    public Task RemoveUserFromGroupAsync(
        string userId, string groupId, CancellationToken cancellationToken = default) =>
        _groupStore.RemoveUserFromGroupAsync(userId, groupId, cancellationToken);

    // ──── IIdentitySessionManager ────
    // ASP.NET Core Identity does not support individual session termination.

    /// <inheritdoc/>
    public Task<IReadOnlyList<IdentitySession>> GetUserSessionsAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IdentitySession>>([]);

    /// <inheritdoc/>
    public Task<IReadOnlyList<IdentityDeviceActivity>> GetUserDeviceActivityAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IdentityDeviceActivity>>([]);

    /// <inheritdoc/>
    public Task TerminateSessionAsync(
        string userId, string sessionId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    public Task TerminateAllSessionsAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    // ──── IIdentityPasswordManager ────

    /// <inheritdoc/>
    public Task<DateTimeOffset?> GetPasswordChangedAtAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<DateTimeOffset?>(null);

    /// <inheritdoc/>
    // SupportsNativePasswordResetEmail is false — the framework routes through IPasswordResetService instead.
    public Task SendPasswordResetEmailAsync(
        string userId, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    /// <inheritdoc/>
#pragma warning disable GRSEC003 // Method name contains "Password" — not a secret
    public async Task SetTemporaryPasswordAsync(
        string userId, string temporaryPassword, CancellationToken cancellationToken = default)
    {
        GranitUser user = await _userManager.FindByIdAsync(userId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"User {userId} not found.");

        string token = await _userManager.GeneratePasswordResetTokenAsync(user).ConfigureAwait(false);
        IdentityResult result = await _userManager.ResetPasswordAsync(user, token, temporaryPassword).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            string errors = string.Join(", ", result.Errors.Select(e => e.Description));
            Log.IdentityOperationFailed(_logger, "Password reset", errors);
            throw new InvalidOperationException($"Password reset failed: {errors}");
        }
    }
#pragma warning restore GRSEC003

    // ──── IIdentityCredentialVerifier ────

    /// <inheritdoc/>
#pragma warning disable GRSEC003 // Parameter name contains "password" — not a secret
    public async Task<bool> VerifyUserCredentialsAsync(
        string username, string password, CancellationToken cancellationToken = default)
    {
        GranitUser? user = await _userManager.FindByNameAsync(username).ConfigureAwait(false);
        if (user is null)
        {
            return false;
        }

        return await _userManager.CheckPasswordAsync(user, password).ConfigureAwait(false);
    }
#pragma warning restore GRSEC003

    internal static Guid ParseGuid(string value, string parameterName) =>
        Guid.TryParse(value, out Guid result)
            ? result
            : throw new ArgumentException($"'{value}' is not a valid GUID.", parameterName);

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning,
            Message = "{Operation} failed: {Errors}")]
        public static partial void IdentityOperationFailed(
            ILogger logger, string operation, string errors);
    }
}
