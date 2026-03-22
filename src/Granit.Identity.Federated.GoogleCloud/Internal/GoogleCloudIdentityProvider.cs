using System.Diagnostics;
using FirebaseAdmin.Auth;
using Granit.Core.Events;
using Granit.Identity.Events;
using Granit.Identity.Federated.GoogleCloud.Diagnostics;
using Granit.Identity.Federated.GoogleCloud.Options;
using Granit.Identity.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Identity.Federated.GoogleCloud.Internal;

/// <summary>
/// <see cref="IIdentityProvider"/> implementation using Google Cloud Identity Platform (Firebase Auth).
/// </summary>
/// <remarks>
/// Firebase Auth uses custom claims for role management (no native roles/groups).
/// Groups are not supported. Roles are stored in custom claims under the configured key.
/// </remarks>
internal sealed partial class GoogleCloudIdentityProvider(
    IFirebaseAuthTransport transport,
    IOptions<GoogleCloudIdentityOptions> options,
    IDistributedEventBus distributedEventBus,
    ILogger<GoogleCloudIdentityProvider> logger) : IIdentityProvider
{
    private readonly GoogleCloudIdentityOptions _options = options.Value;

    // ── Users ─────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<IIdentityUser>> GetUsersAsync(
        string? search = null, int? first = null, int? max = null,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityGoogleCloudActivitySource.Source.StartActivity(
            IdentityGoogleCloudActivitySource.Operations.ListUsers);

        try
        {
            IReadOnlyList<ExportedUserRecord> records = await transport.ListUsersAsync(cancellationToken).ConfigureAwait(false);
            List<FederatedIdentityUser> users = [];

            foreach (ExportedUserRecord user in records)
            {
                if (search is not null &&
                    !(user.Email?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) &&
                    !(user.DisplayName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    continue;
                }

                users.Add(MapUser(user));
            }

            IEnumerable<FederatedIdentityUser> result = users.AsEnumerable();
            if (first.HasValue)
            {
                result = result.Skip(first.Value);
            }

            if (max.HasValue)
            {
                result = result.Take(max.Value);
            }

            return result.ToList();
        }
        catch (Exception ex)
        {
            LogListUsersFailed(ex);
            return [];
        }
    }

    public async Task<IIdentityUser?> GetUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityGoogleCloudActivitySource.Source.StartActivity(
            IdentityGoogleCloudActivitySource.Operations.GetUser);
        activity?.SetTag(IdentityGoogleCloudActivitySource.Tags.UserId, userId);

        try
        {
            UserRecord user = await transport.GetUserAsync(userId, cancellationToken).ConfigureAwait(false);
            return MapUser(user);
        }
        catch (FirebaseAuthException ex) when (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
        {
            return null;
        }
        catch (Exception ex)
        {
            LogGetUserFailed(userId, ex);
            return null;
        }
    }

    public async Task SetUserEnabledAsync(string userId, bool enabled, CancellationToken cancellationToken = default)
    {
        string operation = enabled
            ? IdentityGoogleCloudActivitySource.Operations.EnableUser
            : IdentityGoogleCloudActivitySource.Operations.DisableUser;
        using Activity? activity = IdentityGoogleCloudActivitySource.Source.StartActivity(operation);
        activity?.SetTag(IdentityGoogleCloudActivitySource.Tags.UserId, userId);

        UserRecordArgs args = new() { Uid = userId, Disabled = !enabled };
        await transport.UpdateUserAsync(args, cancellationToken).ConfigureAwait(false);

        await distributedEventBus.PublishAsync(new IdentityUserEnabledChangedEto(userId, enabled), cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateUserAsync(string userId, IdentityUserUpdate update, CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityGoogleCloudActivitySource.Source.StartActivity(
            IdentityGoogleCloudActivitySource.Operations.UpdateUser);
        activity?.SetTag(IdentityGoogleCloudActivitySource.Tags.UserId, userId);

        UserRecordArgs args = new() { Uid = userId };

        if (update.Email is not null)
        {
            args.Email = update.Email;
        }

        string? displayName = BuildDisplayName(update.FirstName, update.LastName);
        if (displayName is not null)
        {
            args.DisplayName = displayName;
        }

        await transport.UpdateUserAsync(args, cancellationToken).ConfigureAwait(false);

        await distributedEventBus.PublishAsync(new IdentityUserProfileUpdatedEto(userId, update), cancellationToken).ConfigureAwait(false);
    }

    public async Task<IIdentityUser> CreateUserAsync(IdentityUserCreate user, CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityGoogleCloudActivitySource.Source.StartActivity(
            IdentityGoogleCloudActivitySource.Operations.CreateUser);

        UserRecordArgs args = new()
        {
            Email = user.Email,
            DisplayName = BuildDisplayName(user.FirstName, user.LastName) ?? user.Username,
            Disabled = !user.Enabled,
        };

        if (!string.IsNullOrEmpty(user.TemporaryPassword))
        {
            args.Password = user.TemporaryPassword;
        }

        UserRecord created = await transport.CreateUserAsync(args, cancellationToken).ConfigureAwait(false);

        await distributedEventBus.PublishAsync(new IdentityUserCreatedEto(created.Uid, user.Username, user.Email), cancellationToken).ConfigureAwait(false);

        return new FederatedIdentityUser(
            UserId: created.Uid,
            Username: created.DisplayName ?? created.Email ?? created.Uid,
            Email: created.Email ?? string.Empty,
            FirstName: ExtractFirstName(created.DisplayName),
            LastName: ExtractLastName(created.DisplayName),
            Enabled: !created.Disabled);
    }

    // ── Roles (custom claims) ─────────────────────────────────────────

    public Task<IReadOnlyList<IdentityRole>> GetRolesAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IdentityRole>>([]);

    public Task<IReadOnlyList<IIdentityUser>> GetRoleMembersAsync(string roleName, CancellationToken cancellationToken = default) =>
        // Not efficiently queryable in Firebase Auth. Return empty.
        Task.FromResult<IReadOnlyList<IIdentityUser>>([]);

    public async Task<IReadOnlyList<IdentityRole>> GetUserRolesAsync(string userId, CancellationToken cancellationToken = default)
    {
        try
        {
            UserRecord user = await transport.GetUserAsync(userId, cancellationToken).ConfigureAwait(false);
            List<string> roleNames = ExtractRoleNames(user.CustomClaims);
            return roleNames.Select(name => new IdentityRole(name, name, null)).ToList();
        }
        catch (Exception ex)
        {
            LogGetUserRolesFailed(userId, ex);
            return [];
        }
    }

    public async Task AssignRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityGoogleCloudActivitySource.Source.StartActivity(
            IdentityGoogleCloudActivitySource.Operations.SetCustomClaims);
        activity?.SetTag(IdentityGoogleCloudActivitySource.Tags.UserId, userId);

        UserRecord user = await transport.GetUserAsync(userId, cancellationToken).ConfigureAwait(false);
        List<string> roles = [.. ExtractRoleNames(user.CustomClaims)];

        if (!roles.Contains(roleName))
        {
            roles.Add(roleName);
        }

        Dictionary<string, object> claims = new(user.CustomClaims ?? new Dictionary<string, object>())
        {
            [_options.RolesClaimKey] = roles,
        };

        await transport.SetCustomUserClaimsAsync(userId, claims, cancellationToken).ConfigureAwait(false);

        await distributedEventBus.PublishAsync(new IdentityRoleAssignedEto(userId, roleName), cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveRoleAsync(string userId, string roleName, CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityGoogleCloudActivitySource.Source.StartActivity(
            IdentityGoogleCloudActivitySource.Operations.SetCustomClaims);
        activity?.SetTag(IdentityGoogleCloudActivitySource.Tags.UserId, userId);

        UserRecord user = await transport.GetUserAsync(userId, cancellationToken).ConfigureAwait(false);
        List<string> roles = [.. ExtractRoleNames(user.CustomClaims)];
        roles.Remove(roleName);

        Dictionary<string, object> claims = new(user.CustomClaims ?? new Dictionary<string, object>())
        {
            [_options.RolesClaimKey] = roles,
        };

        await transport.SetCustomUserClaimsAsync(userId, claims, cancellationToken).ConfigureAwait(false);

        await distributedEventBus.PublishAsync(new IdentityRoleRemovedEto(userId, roleName), cancellationToken).ConfigureAwait(false);
    }

    // ── Groups (not supported by Firebase Auth) ───────────────────────

    public Task<IReadOnlyList<IdentityGroup>> GetGroupsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IdentityGroup>>([]);

    public Task<IReadOnlyList<IdentityGroup>> GetUserGroupsAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IdentityGroup>>([]);

    public Task AddUserToGroupAsync(string userId, string groupId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Firebase Auth does not support groups.");

    public Task RemoveUserFromGroupAsync(string userId, string groupId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Firebase Auth does not support groups.");

    // ── Sessions ──────────────────────────────────────────────────────

    public Task<IReadOnlyList<IdentitySession>> GetUserSessionsAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IdentitySession>>([]);

    public Task<IReadOnlyList<IdentityDeviceActivity>> GetUserDeviceActivityAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IdentityDeviceActivity>>([]);

    public Task TerminateSessionAsync(string userId, string sessionId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Firebase Auth does not support individual session termination.");

    public async Task TerminateAllSessionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityGoogleCloudActivitySource.Source.StartActivity(
            IdentityGoogleCloudActivitySource.Operations.RevokeTokens);
        activity?.SetTag(IdentityGoogleCloudActivitySource.Tags.UserId, userId);

        await transport.RevokeRefreshTokensAsync(userId, cancellationToken).ConfigureAwait(false);

        await distributedEventBus.PublishAsync(new IdentitySessionsRevokedEto(userId), cancellationToken).ConfigureAwait(false);
    }

    // ── Password ──────────────────────────────────────────────────────

    public Task<DateTimeOffset?> GetPasswordChangedAtAsync(string userId, CancellationToken cancellationToken = default) =>
        Task.FromResult<DateTimeOffset?>(null);

    public async Task SendPasswordResetEmailAsync(string userId, CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityGoogleCloudActivitySource.Source.StartActivity(
            IdentityGoogleCloudActivitySource.Operations.GeneratePasswordResetLink);
        activity?.SetTag(IdentityGoogleCloudActivitySource.Tags.UserId, userId);

        UserRecord user = await transport.GetUserAsync(userId, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrEmpty(user.Email))
        {
            throw new InvalidOperationException($"User {userId} has no email address.");
        }

        await transport.GeneratePasswordResetLinkAsync(user.Email, cancellationToken).ConfigureAwait(false);

        await distributedEventBus.PublishAsync(new IdentityPasswordResetEto(userId), cancellationToken).ConfigureAwait(false);
    }

    public async Task SetTemporaryPasswordAsync(string userId, string temporaryPassword, CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityGoogleCloudActivitySource.Source.StartActivity(
            IdentityGoogleCloudActivitySource.Operations.SetPassword);
        activity?.SetTag(IdentityGoogleCloudActivitySource.Tags.UserId, userId);

        UserRecordArgs args = new() { Uid = userId, Password = temporaryPassword };
        await transport.UpdateUserAsync(args, cancellationToken).ConfigureAwait(false);
    }

    // ── Credentials ───────────────────────────────────────────────────

    public async Task<bool> VerifyUserCredentialsAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityGoogleCloudActivitySource.Source.StartActivity(
            IdentityGoogleCloudActivitySource.Operations.VerifyCredentials);

        return await transport.VerifyPasswordAsync(username, password, cancellationToken).ConfigureAwait(false);
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static FederatedIdentityUser MapUser(UserRecord user) =>
        new(
            UserId: user.Uid,
            Username: user.DisplayName ?? user.Email ?? user.Uid,
            Email: user.Email ?? string.Empty,
            FirstName: ExtractFirstName(user.DisplayName),
            LastName: ExtractLastName(user.DisplayName),
            Enabled: !user.Disabled,
            ExtraProperties: user.CustomClaims?.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty));

    private List<string> ExtractRoleNames(IReadOnlyDictionary<string, object>? customClaims)
    {
        if (customClaims is null || !customClaims.TryGetValue(_options.RolesClaimKey, out object? rolesObj))
        {
            return [];
        }

        if (rolesObj is IEnumerable<object> enumerable)
        {
            return enumerable.Select(r => r.ToString() ?? string.Empty).Where(r => r.Length > 0).ToList();
        }

        if (rolesObj is Newtonsoft.Json.Linq.JArray jArray)
        {
            return jArray.Select(t => t.ToString()).Where(r => r.Length > 0).ToList();
        }

        return [];
    }

    private static string ExtractFirstName(string? displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            return string.Empty;
        }

        int spaceIndex = displayName.IndexOf(' ');
        return spaceIndex > 0 ? displayName[..spaceIndex] : displayName;
    }

    private static string ExtractLastName(string? displayName)
    {
        if (string.IsNullOrEmpty(displayName))
        {
            return string.Empty;
        }

        int spaceIndex = displayName.IndexOf(' ');
        return spaceIndex > 0 ? displayName[(spaceIndex + 1)..] : string.Empty;
    }

    private static string? BuildDisplayName(string? firstName, string? lastName) =>
        (firstName, lastName) switch
        {
            (not null, not null) => $"{firstName} {lastName}",
            (not null, null) => firstName,
            (null, not null) => lastName,
            _ => null,
        };

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to list users from Firebase Auth")]
    private partial void LogListUsersFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get user {UserId} from Firebase Auth")]
    private partial void LogGetUserFailed(string userId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get roles for user {UserId} from Firebase Auth")]
    private partial void LogGetUserRolesFailed(string userId, Exception exception);
}
