using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Granit.Core.Events;
using Granit.Identity.EntraId.Diagnostics;
using Granit.Identity.EntraId.Options;
using Granit.Identity.Events;
using Granit.Identity.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Identity.EntraId.Internal;

/// <summary>
/// <see cref="IIdentityProvider"/> implementation that queries the Microsoft Graph API v1.0.
/// </summary>
/// <remarks>
/// <para>
/// Read operations follow graceful degradation: if Graph API is unreachable, logs a warning
/// and returns empty results instead of propagating the exception.
/// </para>
/// <para>
/// Write operations propagate exceptions so callers can handle failures explicitly.
/// </para>
/// <para>
/// All public methods emit OpenTelemetry spans via <see cref="IdentityEntraIdActivitySource"/>.
/// </para>
/// </remarks>
internal sealed partial class EntraIdIdentityProvider(
    EntraIdAdminTokenService tokenService,
    IHttpClientFactory httpClientFactory,
    IOptions<EntraIdAdminOptions> options,
    IPasswordResetNotifier passwordResetNotifier,
    IDistributedEventBus distributedEventBus,
    ILogger<EntraIdIdentityProvider> logger) : IIdentityProvider
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityUser>> GetUsersAsync(
        string? search = null,
        int? first = null,
        int? max = null,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.GetUsers);

        try
        {
            HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
            string endpoint = EntraIdAdminOptions.GetUsersEndpoint(search, first, max);

            GraphCollectionResponse<GraphUserRepresentation>? response = await client
                .GetFromJsonAsync<GraphCollectionResponse<GraphUserRepresentation>>(endpoint, cancellationToken)
                .ConfigureAwait(false);

            return response?.Value?.ConvertAll(ToIdentityUser) ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogGetUsersFailed(ex);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<IdentityUser?> GetUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.GetUser);
        activity?.SetTag(IdentityEntraIdActivitySource.TagUserId, userId);

        try
        {
            HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
            string endpoint = EntraIdAdminOptions.GetUserEndpoint(userId);

            GraphUserRepresentation? user = await client
                .GetFromJsonAsync<GraphUserRepresentation>(endpoint, cancellationToken)
                .ConfigureAwait(false);

            return user is not null ? ToIdentityUser(user) : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogGetUserFailed(ex, userId);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task SetUserEnabledAsync(
        string userId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.SetUserEnabled);
        activity?.SetTag(IdentityEntraIdActivitySource.TagUserId, userId);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
        string endpoint = $"/v1.0/users/{Uri.EscapeDataString(userId)}";

        using HttpResponseMessage response = await client.PatchAsJsonAsync(
            endpoint, new { accountEnabled = enabled }, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await distributedEventBus.PublishAsync(new IdentityUserEnabledChangedEto(userId, enabled), cancellationToken).ConfigureAwait(false);

        LogUserEnabledChanged(userId, enabled ? "enabled" : "disabled");
    }

    /// <inheritdoc/>
    public async Task UpdateUserAsync(
        string userId,
        IdentityUserUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(update);

        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.UpdateUser);
        activity?.SetTag(IdentityEntraIdActivitySource.TagUserId, userId);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
        string endpoint = $"/v1.0/users/{Uri.EscapeDataString(userId)}";

        var payload = new Dictionary<string, object?>();

        if (update.Email is not null)
        {
            payload["mail"] = update.Email;
        }

        if (update.FirstName is not null)
        {
            payload["givenName"] = update.FirstName;
        }

        if (update.LastName is not null)
        {
            payload["surname"] = update.LastName;
        }

        // Map custom attributes to onPremisesExtensionAttributes
        if (update.Attributes is { Count: > 0 })
        {
            payload["onPremisesExtensionAttributes"] = MapAttributesToExtensions(update.Attributes);
        }

        using HttpResponseMessage response = await client
            .PatchAsJsonAsync(endpoint, payload, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await distributedEventBus.PublishAsync(new IdentityUserProfileUpdatedEto(userId, update), cancellationToken).ConfigureAwait(false);

        LogUserProfileUpdated(userId);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentitySession>> GetUserSessionsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.GetUserSessions);
        activity?.SetTag(IdentityEntraIdActivitySource.TagUserId, userId);

        try
        {
            HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
            string endpoint = EntraIdAdminOptions.GetAuditSignInsEndpoint(userId);

            GraphCollectionResponse<GraphAuditSignInRepresentation>? response = await client
                .GetFromJsonAsync<GraphCollectionResponse<GraphAuditSignInRepresentation>>(endpoint, cancellationToken)
                .ConfigureAwait(false);

            return response?.Value?
                .Where(s => s.Status?.ErrorCode == 0) // Only successful sign-ins
                .Select(ToIdentitySession)
                .ToList() ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogGetSessionsFailed(ex, userId);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityDeviceActivity>> GetUserDeviceActivityAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.GetUserDeviceActivity);
        activity?.SetTag(IdentityEntraIdActivitySource.TagUserId, userId);

        try
        {
            HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
            string endpoint = EntraIdAdminOptions.GetAuditSignInsEndpoint(userId, top: 50);

            GraphCollectionResponse<GraphAuditSignInRepresentation>? response = await client
                .GetFromJsonAsync<GraphCollectionResponse<GraphAuditSignInRepresentation>>(endpoint, cancellationToken)
                .ConfigureAwait(false);

            List<GraphAuditSignInRepresentation> signIns = response?.Value?
                .Where(s => s.Status?.ErrorCode == 0)
                .ToList() ?? [];

            // Group by IP + OS to produce device activity entries
            return signIns
                .GroupBy(s => new
                {
                    Ip = s.IpAddress ?? "unknown",
                    Os = s.DeviceDetail?.OperatingSystem ?? "unknown"
                })
                .Select(group =>
                {
                    GraphAuditSignInRepresentation latest = group.OrderByDescending(s => s.CreatedDateTime).First();
                    string? os = latest.DeviceDetail?.OperatingSystem;
                    bool mobile = IsMobileOs(os);

                    return new IdentityDeviceActivity(
                        IpAddress: latest.IpAddress,
                        LastAccess: latest.CreatedDateTime ?? DateTimeOffset.MinValue,
                        Device: mobile ? "Mobile" : "Desktop",
                        Os: os,
                        OsVersion: null,
                        Browser: latest.DeviceDetail?.Browser,
                        Mobile: mobile,
                        Current: false,
                        Sessions: group.Select(ToIdentitySession).ToList());
                })
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogGetDeviceActivityFailed(ex, userId);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<DateTimeOffset?> GetPasswordChangedAtAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.GetPasswordChangedAt);
        activity?.SetTag(IdentityEntraIdActivitySource.TagUserId, userId);

        try
        {
            HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
            string endpoint = EntraIdAdminOptions.GetUserPasswordChangeDateEndpoint(userId);

            GraphUserRepresentation? user = await client
                .GetFromJsonAsync<GraphUserRepresentation>(endpoint, cancellationToken)
                .ConfigureAwait(false);

            return user?.LastPasswordChangeDateTime;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogGetPasswordChangedAtFailed(ex, userId);
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityRole>> GetRolesAsync(
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.GetRoles);

        try
        {
            HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
            string endpoint = options.Value.GetServicePrincipalAppRolesEndpoint();

            // The appRoles endpoint returns the SP resource directly, not a collection
            GraphServicePrincipalRepresentation? sp = await client
                .GetFromJsonAsync<GraphServicePrincipalRepresentation>(endpoint, cancellationToken)
                .ConfigureAwait(false);

            return sp?.AppRoles?
                .Where(r => r.IsEnabled)
                .Select(r => new IdentityRole(r.Id, r.Value, r.Description))
                .ToList() ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogGetRolesFailed(ex);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityUser>> GetRoleMembersAsync(
        string roleName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roleName);

        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.GetRoleMembers);
        activity?.SetTag(IdentityEntraIdActivitySource.TagRoleName, roleName);

        try
        {
            HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);

            // Get all App Role assignments on the Service Principal
            string endpoint = options.Value.GetServicePrincipalAppRoleAssignedToEndpoint();

            GraphCollectionResponse<GraphAppRoleAssignmentRepresentation>? assignments = await client
                .GetFromJsonAsync<GraphCollectionResponse<GraphAppRoleAssignmentRepresentation>>(endpoint, cancellationToken)
                .ConfigureAwait(false);

            // Resolve the role name to an App Role ID
            string? roleId = await ResolveRoleIdByNameAsync(client, roleName, cancellationToken).ConfigureAwait(false);
            if (roleId is null)
            {
                return [];
            }

            // Filter assignments by role ID and resolve each user
            List<string> userIds = assignments?.Value?
                .Where(a => string.Equals(a.AppRoleId, roleId, StringComparison.OrdinalIgnoreCase))
                .Select(a => a.PrincipalId)
                .ToList() ?? [];

            List<IdentityUser> users = [];
            foreach (string uid in userIds)
            {
                IdentityUser? user = await GetUserAsync(uid, cancellationToken).ConfigureAwait(false);
                if (user is not null)
                {
                    users.Add(user);
                }
            }

            return users;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogGetRoleMembersFailed(ex, roleName);
            return [];
        }
    }

    // ──── User role management ────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityRole>> GetUserRolesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.GetUserRoles);
        activity?.SetTag(IdentityEntraIdActivitySource.TagUserId, userId);

        try
        {
            HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
            string endpoint = EntraIdAdminOptions.GetUserAppRoleAssignmentsEndpoint(userId);

            GraphCollectionResponse<GraphAppRoleAssignmentRepresentation>? assignments = await client
                .GetFromJsonAsync<GraphCollectionResponse<GraphAppRoleAssignmentRepresentation>>(endpoint, cancellationToken)
                .ConfigureAwait(false);

            if (assignments?.Value is not { Count: > 0 })
            {
                return [];
            }

            // Cross-reference with App Role definitions to get names
            List<GraphAppRoleRepresentation> appRoles = await GetAppRolesAsync(client, cancellationToken).ConfigureAwait(false);
            var roleMap = appRoles.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);

            return assignments.Value
                .Where(a => roleMap.ContainsKey(a.AppRoleId))
                .Select(a =>
                {
                    GraphAppRoleRepresentation role = roleMap[a.AppRoleId];
                    return new IdentityRole(role.Id, role.Value, role.Description);
                })
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogGetUserRolesFailed(ex, userId);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task AssignRoleAsync(
        string userId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(roleName);

        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.AssignRole);
        activity?.SetTag(IdentityEntraIdActivitySource.TagUserId, userId);
        activity?.SetTag(IdentityEntraIdActivitySource.TagRoleName, roleName);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);

        string roleId = await ResolveRoleIdByNameAsync(client, roleName, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"App Role '{roleName}' not found on service principal.");

        string endpoint = EntraIdAdminOptions.GetUserAppRoleAssignmentsEndpoint(userId);
        EntraIdAdminOptions opts = options.Value;

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            endpoint,
            new
            {
                principalId = userId,
                resourceId = opts.ServicePrincipalObjectId,
                appRoleId = roleId
            },
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await distributedEventBus.PublishAsync(new IdentityRoleAssignedEto(userId, roleName), cancellationToken).ConfigureAwait(false);

        LogRoleAssigned(roleName, userId);
    }

    /// <inheritdoc/>
    public async Task RemoveRoleAsync(
        string userId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(roleName);

        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.RemoveRole);
        activity?.SetTag(IdentityEntraIdActivitySource.TagUserId, userId);
        activity?.SetTag(IdentityEntraIdActivitySource.TagRoleName, roleName);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);

        // Find the assignment to delete
        string assignmentsEndpoint = EntraIdAdminOptions.GetUserAppRoleAssignmentsEndpoint(userId);

        GraphCollectionResponse<GraphAppRoleAssignmentRepresentation>? assignments = await client
            .GetFromJsonAsync<GraphCollectionResponse<GraphAppRoleAssignmentRepresentation>>(assignmentsEndpoint, cancellationToken)
            .ConfigureAwait(false);

        string? roleId = await ResolveRoleIdByNameAsync(client, roleName, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"App Role '{roleName}' not found on service principal.");

        GraphAppRoleAssignmentRepresentation? assignment = assignments?.Value?
            .Find(a => string.Equals(a.AppRoleId, roleId, StringComparison.OrdinalIgnoreCase));

        if (assignment is null)
        {
            throw new InvalidOperationException($"User '{userId}' does not have App Role '{roleName}'.");
        }

        string deleteEndpoint = $"{assignmentsEndpoint}/{Uri.EscapeDataString(assignment.Id)}";

        using HttpResponseMessage response = await client
            .DeleteAsync(deleteEndpoint, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await distributedEventBus.PublishAsync(new IdentityRoleRemovedEto(userId, roleName), cancellationToken).ConfigureAwait(false);

        LogRoleRemoved(roleName, userId);
    }

    // ──── Session termination ────

    /// <inheritdoc/>
    public async Task TerminateSessionAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(sessionId);

        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.TerminateSession);
        activity?.SetTag(IdentityEntraIdActivitySource.TagUserId, userId);

        // Entra ID does not support individual session termination.
        // We revoke all sessions and log a warning.
        LogIndividualSessionTerminationNotSupported(sessionId, userId);

        await TerminateAllSessionsAsync(userId, cancellationToken).ConfigureAwait(false);

        await distributedEventBus.PublishAsync(new IdentitySessionsRevokedEto(userId), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task TerminateAllSessionsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.TerminateAllSessions);
        activity?.SetTag(IdentityEntraIdActivitySource.TagUserId, userId);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
        string endpoint = EntraIdAdminOptions.GetRevokeSessionsEndpoint(userId);

        using HttpResponseMessage response = await client
            .PostAsync(endpoint, content: null, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await distributedEventBus.PublishAsync(new IdentitySessionsRevokedEto(userId), cancellationToken).ConfigureAwait(false);

        LogAllSessionsTerminated(userId);
    }

    // ──── Password management ────

    /// <inheritdoc/>
    public async Task SendPasswordResetEmailAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.SendPasswordResetEmail);
        activity?.SetTag(IdentityEntraIdActivitySource.TagUserId, userId);

        // Entra ID does not support sending password reset emails via Graph API.
        // We generate a temporary password, set it, then notify via the optional hook.
        string temporaryPassword = GenerateTemporaryPassword();

        await SetTemporaryPasswordAsync(userId, temporaryPassword, cancellationToken).ConfigureAwait(false);
        await passwordResetNotifier.NotifyAsync(userId, temporaryPassword, cancellationToken).ConfigureAwait(false);

        await distributedEventBus.PublishAsync(new IdentityPasswordResetEto(userId), cancellationToken).ConfigureAwait(false);

        LogPasswordResetWithNotifier(userId);
    }

    /// <inheritdoc/>
    public async Task SetTemporaryPasswordAsync(
        string userId,
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(temporaryPassword);

        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.SetTemporaryPassword);
        activity?.SetTag(IdentityEntraIdActivitySource.TagUserId, userId);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
        string endpoint = $"/v1.0/users/{Uri.EscapeDataString(userId)}";

        using HttpResponseMessage response = await client.PatchAsJsonAsync(
            endpoint,
            new
            {
                passwordProfile = new
                {
                    password = temporaryPassword,
                    forceChangePasswordNextSignIn = true
                }
            },
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await distributedEventBus.PublishAsync(new IdentityPasswordResetEto(userId), cancellationToken).ConfigureAwait(false);

        LogTemporaryPasswordSet(userId);
    }

    // ──── User creation ────

    /// <inheritdoc/>
    public async Task<IdentityUser> CreateUserAsync(
        IdentityUserCreate user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.CreateUser);

        EntraIdAdminOptions opts = options.Value;

        if (string.IsNullOrEmpty(opts.DefaultDomain))
        {
            throw new InvalidOperationException(
                $"{nameof(EntraIdAdminOptions)}.{nameof(EntraIdAdminOptions.DefaultDomain)} " +
                "must be configured to create users in Entra ID.");
        }

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);

        string upn = user.Username.Contains('@')
            ? user.Username
            : $"{user.Username}@{opts.DefaultDomain}";

        var payload = new
        {
            accountEnabled = user.Enabled,
            displayName = $"{user.FirstName} {user.LastName}".Trim(),
            mailNickname = user.Username.Split('@')[0],
            userPrincipalName = upn,
            mail = user.Email,
            givenName = user.FirstName,
            surname = user.LastName,
            passwordProfile = user.TemporaryPassword is not null
                ? new { password = user.TemporaryPassword, forceChangePasswordNextSignIn = true }
                : null
        };

        using HttpResponseMessage response = await client
            .PostAsJsonAsync("/v1.0/users", payload, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        GraphUserRepresentation? created = await response.Content
            .ReadFromJsonAsync<GraphUserRepresentation>(cancellationToken)
            .ConfigureAwait(false);

        string createdUserId = created?.Id
            ?? throw new InvalidOperationException("Entra ID did not return a user ID after creation.");

        activity?.SetTag(IdentityEntraIdActivitySource.TagUserId, createdUserId);
        LogUserCreated(user.Username, createdUserId);

        var createdIdentityUser = new IdentityUser(
            createdUserId,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.Enabled);

        await distributedEventBus.PublishAsync(new IdentityUserCreatedEto(createdIdentityUser.Id, createdIdentityUser.Username, createdIdentityUser.Email), cancellationToken).ConfigureAwait(false);

        return createdIdentityUser;
    }

    // ──── Group management ────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityGroup>> GetGroupsAsync(
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.GetGroups);

        try
        {
            HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
            string endpoint = EntraIdAdminOptions.GroupsEndpoint;

            GraphCollectionResponse<GraphGroupRepresentation>? response = await client
                .GetFromJsonAsync<GraphCollectionResponse<GraphGroupRepresentation>>(endpoint, cancellationToken)
                .ConfigureAwait(false);

            return response?.Value?.ConvertAll(ToIdentityGroup) ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogGetGroupsFailed(ex);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityGroup>> GetUserGroupsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.GetUserGroups);
        activity?.SetTag(IdentityEntraIdActivitySource.TagUserId, userId);

        try
        {
            HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
            string endpoint = EntraIdAdminOptions.GetUserGroupsEndpoint(userId);

            GraphCollectionResponse<GraphGroupRepresentation>? response = await client
                .GetFromJsonAsync<GraphCollectionResponse<GraphGroupRepresentation>>(endpoint, cancellationToken)
                .ConfigureAwait(false);

            return response?.Value?.ConvertAll(ToIdentityGroup) ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            LogGetUserGroupsFailed(ex, userId);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task AddUserToGroupAsync(
        string userId,
        string groupId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(groupId);

        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.AddUserToGroup);
        activity?.SetTag(IdentityEntraIdActivitySource.TagUserId, userId);
        activity?.SetTag(IdentityEntraIdActivitySource.TagGroupId, groupId);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
        string endpoint = EntraIdAdminOptions.GetGroupMembersRefEndpoint(groupId);

        using HttpResponseMessage response = await client.PostAsJsonAsync(
            endpoint,
            new { odataId = $"{options.Value.GraphBaseUrl}/v1.0/directoryObjects/{userId}" },
            cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await distributedEventBus.PublishAsync(new IdentityGroupMembershipChangedEto(userId, groupId, Added: true), cancellationToken).ConfigureAwait(false);

        LogUserAddedToGroup(userId, groupId);
    }

    /// <inheritdoc/>
    public async Task RemoveUserFromGroupAsync(
        string userId,
        string groupId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(groupId);

        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.RemoveUserFromGroup);
        activity?.SetTag(IdentityEntraIdActivitySource.TagUserId, userId);
        activity?.SetTag(IdentityEntraIdActivitySource.TagGroupId, groupId);

        HttpClient client = await CreateAuthenticatedClientAsync(cancellationToken).ConfigureAwait(false);
        string endpoint = EntraIdAdminOptions.GetGroupMemberEndpoint(groupId, userId);

        using HttpResponseMessage response = await client
            .DeleteAsync(endpoint, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await distributedEventBus.PublishAsync(new IdentityGroupMembershipChangedEto(userId, groupId, Added: false), cancellationToken).ConfigureAwait(false);

        LogUserRemovedFromGroup(userId, groupId);
    }

    // ──── Credential verification ────

    /// <inheritdoc/>
    public async Task<bool> VerifyUserCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(username);
        ArgumentNullException.ThrowIfNull(password);

        using Activity? activity = IdentityEntraIdActivitySource.Source.StartActivity(IdentityEntraIdActivitySource.VerifyUserCredentials);

        EntraIdAdminOptions opts = options.Value;

        if (string.IsNullOrEmpty(opts.RopcClientId))
        {
            throw new InvalidOperationException(
                $"{nameof(EntraIdAdminOptions)}.{nameof(EntraIdAdminOptions.RopcClientId)} " +
                "must be configured to use credential verification.");
        }

        HttpClient client = httpClientFactory.CreateClient("MicrosoftGraph");

        using FormUrlEncodedContent content = new(
        [
            new KeyValuePair<string, string>("grant_type", "password"),
            new KeyValuePair<string, string>("client_id", opts.RopcClientId),
            new KeyValuePair<string, string>("username", username),
            new KeyValuePair<string, string>("password", password),
            new KeyValuePair<string, string>("scope", "openid"),
        ]);

        using HttpResponseMessage response = await client
            .PostAsync(opts.GetTokenEndpoint(), content, cancellationToken)
            .ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
        {
            LogCredentialVerificationSucceeded(username);
            return true;
        }

        LogCredentialVerificationFailed(username, (int)response.StatusCode);
        return false;
    }

    // ──── Private helpers ────

    private async Task<HttpClient> CreateAuthenticatedClientAsync(CancellationToken cancellationToken)
    {
        string token = await tokenService.GetTokenAsync(cancellationToken).ConfigureAwait(false);
        HttpClient client = httpClientFactory.CreateClient("MicrosoftGraph");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static IdentityUser ToIdentityUser(GraphUserRepresentation user) =>
        new(user.Id, user.UserPrincipalName, user.Mail, user.GivenName, user.Surname, user.AccountEnabled,
            ExtractExtensionAttributes(user.ExtensionAttributes));

    private static IdentitySession ToIdentitySession(GraphAuditSignInRepresentation signIn) =>
        new(
            SessionId: signIn.Id,
            IpAddress: signIn.IpAddress,
            StartedAt: signIn.CreatedDateTime ?? DateTimeOffset.MinValue,
            LastAccess: signIn.CreatedDateTime ?? DateTimeOffset.MinValue,
            RememberMe: false,
            Clients: signIn.ClientAppUsed is not null ? [signIn.ClientAppUsed] : []);

    private static IdentityGroup ToIdentityGroup(GraphGroupRepresentation group) =>
        new(
            Id: group.Id,
            Name: group.DisplayName,
            Path: null, // Entra ID groups are flat — no hierarchy
            SubGroups: []); // Entra ID groups have no sub-groups

    private static Dictionary<string, string>? ExtractExtensionAttributes(GraphExtensionAttributes? attrs)
    {
        if (attrs is null)
        {
            return null;
        }

        Dictionary<string, string> result = new(StringComparer.Ordinal);
        string?[] values =
        [
            attrs.ExtensionAttribute1, attrs.ExtensionAttribute2, attrs.ExtensionAttribute3,
            attrs.ExtensionAttribute4, attrs.ExtensionAttribute5, attrs.ExtensionAttribute6,
            attrs.ExtensionAttribute7, attrs.ExtensionAttribute8, attrs.ExtensionAttribute9,
            attrs.ExtensionAttribute10, attrs.ExtensionAttribute11, attrs.ExtensionAttribute12,
            attrs.ExtensionAttribute13, attrs.ExtensionAttribute14, attrs.ExtensionAttribute15,
        ];

        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] is not null)
            {
                result[$"extensionAttribute{i + 1}"] = values[i]!;
            }
        }

        return result.Count > 0 ? result : null;
    }

    private static Dictionary<string, string?> MapAttributesToExtensions(IReadOnlyDictionary<string, string?> attributes)
    {
        Dictionary<string, string?> extensions = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string?> attr in attributes)
        {
            // Accept both "extensionAttribute1" format and plain numeric index
            extensions[attr.Key] = attr.Value;
        }

        return extensions;
    }

    private async Task<List<GraphAppRoleRepresentation>> GetAppRolesAsync(
        HttpClient client, CancellationToken cancellationToken)
    {
        string endpoint = options.Value.GetServicePrincipalAppRolesEndpoint();

        GraphServicePrincipalRepresentation? sp = await client
            .GetFromJsonAsync<GraphServicePrincipalRepresentation>(endpoint, cancellationToken)
            .ConfigureAwait(false);

        return sp?.AppRoles?.Where(r => r.IsEnabled).ToList() ?? [];
    }

    private async Task<string?> ResolveRoleIdByNameAsync(
        HttpClient client, string roleName, CancellationToken cancellationToken)
    {
        List<GraphAppRoleRepresentation> appRoles = await GetAppRolesAsync(client, cancellationToken).ConfigureAwait(false);
        return appRoles.Find(r => string.Equals(r.Value, roleName, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    private static bool IsMobileOs(string? os) =>
        os is not null && (
            os.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
            os.Contains("iOS", StringComparison.OrdinalIgnoreCase) ||
            os.Contains("iPadOS", StringComparison.OrdinalIgnoreCase));

    private static string GenerateTemporaryPassword()
    {
        Span<byte> randomBytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(randomBytes);
        return string.Create(16, randomBytes.ToArray(), static (span, bytes) =>
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%";
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = chars[bytes[i] % chars.Length];
            }
        });
    }

    // ──── Source-generated log messages ────

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get users from Entra ID. Returning empty list")]
    private partial void LogGetUsersFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get user {UserId} from Entra ID. Returning null")]
    private partial void LogGetUserFailed(Exception exception, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} {Action} in Entra ID")]
    private partial void LogUserEnabledChanged(string userId, string action);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} profile updated in Entra ID")]
    private partial void LogUserProfileUpdated(string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get sessions for user {UserId} from Entra ID. Returning empty list")]
    private partial void LogGetSessionsFailed(Exception exception, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get device activity for user {UserId} from Entra ID. Returning empty list")]
    private partial void LogGetDeviceActivityFailed(Exception exception, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get password change date for user {UserId} from Entra ID. Returning null")]
    private partial void LogGetPasswordChangedAtFailed(Exception exception, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get roles from Entra ID. Returning empty list")]
    private partial void LogGetRolesFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get members of role {RoleName} from Entra ID. Returning empty list")]
    private partial void LogGetRoleMembersFailed(Exception exception, string roleName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get roles for user {UserId} from Entra ID. Returning empty list")]
    private partial void LogGetUserRolesFailed(Exception exception, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Role {RoleName} assigned to user {UserId} in Entra ID")]
    private partial void LogRoleAssigned(string roleName, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Role {RoleName} removed from user {UserId} in Entra ID")]
    private partial void LogRoleRemoved(string roleName, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Entra ID does not support individual session termination. Session {SessionId} ignored — revoking ALL sessions for user {UserId}")]
    private partial void LogIndividualSessionTerminationNotSupported(string sessionId, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "All sessions terminated for user {UserId} in Entra ID")]
    private partial void LogAllSessionsTerminated(string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset with notifier for user {UserId} in Entra ID")]
    private partial void LogPasswordResetWithNotifier(string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Temporary password set for user {UserId} in Entra ID")]
    private partial void LogTemporaryPasswordSet(string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {Username} created with ID {UserId} in Entra ID")]
    private partial void LogUserCreated(string username, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get groups from Entra ID. Returning empty list")]
    private partial void LogGetGroupsFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get groups for user {UserId} from Entra ID. Returning empty list")]
    private partial void LogGetUserGroupsFailed(Exception exception, string userId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} added to group {GroupId} in Entra ID")]
    private partial void LogUserAddedToGroup(string userId, string groupId);

    [LoggerMessage(Level = LogLevel.Information, Message = "User {UserId} removed from group {GroupId} in Entra ID")]
    private partial void LogUserRemovedFromGroup(string userId, string groupId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Credential verification succeeded for user {Username}")]
    private partial void LogCredentialVerificationSucceeded(string username);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Credential verification failed for user {Username} (HTTP {StatusCode})")]
    private partial void LogCredentialVerificationFailed(string username, int statusCode);
}
