using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Runtime;
using Granit.Events;
using Granit.Identity.Events;
using Granit.Identity.Federated.Cognito.Diagnostics;
using Granit.Identity.Federated.Cognito.Options;
using Granit.Identity.Federated.Exceptions;
using Granit.Identity.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Granit.Identity.Federated.Cognito.Internal;

/// <summary>
/// AWS Cognito User Pools implementation of <see cref="IIdentityProvider"/>.
/// </summary>
internal sealed partial class CognitoIdentityProvider(
    IAmazonCognitoIdentityProvider cognitoClient,
    IOptions<CognitoAdminOptions> options,
    IOptions<CognitoClientRoleSyncOptions> clientRoleSyncOptions,
    IDistributedEventBus distributedEventBus,
    ILogger<CognitoIdentityProvider> logger)
    : IIdentityProvider, IIdentityClientRoleManager, IUserSessionProvider, IUserDeviceProvider
{
    private const string ProviderName = "cognito";
    private const string EmailAttribute = "email";
    private const string GivenNameAttribute = "given_name";
    private const string FamilyNameAttribute = "family_name";

    /// <summary>
    /// Detects authorisation failures wrapped in <see cref="AmazonServiceException"/>.
    /// Cognito returns 401/403 plus the typed <see cref="NotAuthorizedException"/>; we
    /// promote anything carrying those status codes to the federated unauthorized signal.
    /// </summary>
    private static bool IsAuthorizationFailure(AmazonServiceException ex) =>
        ex.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;

    /// <summary>
    /// Classifies a non-authorization Cognito fault into the taxonomy the graceful-degradation
    /// decorator understands: AWS throttling (HTTP 429 or a <c>Throttl*</c>/<c>TooManyRequests</c>
    /// error code) → <see cref="IdentityProviderThrottledException"/>; a missing resource
    /// (<see cref="ResourceNotFoundException"/>/<see cref="UserNotFoundException"/> or HTTP 404) →
    /// <see cref="IdentityProviderNotFoundException"/>; everything else (5xx, timeout, connection
    /// reset) → <see cref="IdentityProviderTransientException"/>. Applied only to the decorated
    /// <see cref="IIdentityProvider"/> read methods; the caller must re-throw genuine cancellation
    /// before reaching here.
    /// </summary>
    private static Exception ClassifyReadFault(Exception ex, string operation) => ex switch
    {
        AmazonServiceException { StatusCode: HttpStatusCode.TooManyRequests } =>
            new IdentityProviderThrottledException(ProviderName, operation, retryAfter: null, ex),
        AmazonServiceException svc when IsThrottlingErrorCode(svc.ErrorCode) =>
            new IdentityProviderThrottledException(ProviderName, operation, retryAfter: null, ex),
        ResourceNotFoundException or UserNotFoundException =>
            new IdentityProviderNotFoundException(ProviderName, operation, ex),
        AmazonServiceException { StatusCode: HttpStatusCode.NotFound } =>
            new IdentityProviderNotFoundException(ProviderName, operation, ex),
        _ => new IdentityProviderTransientException(ProviderName, operation, ex),
    };

    private static bool IsThrottlingErrorCode(string? errorCode) =>
        errorCode is not null
        && (errorCode.Contains("Throttl", StringComparison.OrdinalIgnoreCase)
            || errorCode.Contains("TooManyRequests", StringComparison.OrdinalIgnoreCase));

    private readonly CognitoAdminOptions _options = options.Value;
    private readonly CognitoClientRoleSyncOptions _clientRoleSyncOptions = clientRoleSyncOptions.Value;

    // ── IIdentityUserReader ────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IIdentityUser>> GetUsersAsync(
        string? search = null,
        int? first = null,
        int? max = null,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityCognitoActivitySource.Source.StartActivity(
            IdentityCognitoActivitySource.Operations.ListUsers);

        try
        {
            ListUsersRequest request = new() { UserPoolId = _options.UserPoolId };

            if (!string.IsNullOrEmpty(search))
            {
                // Cognito Filter supports: username, email, phone_number, name, given_name, family_name, preferred_username, cognito:user_status, status, sub
                // Reject anything outside the safe character set: a stray quote in the search
                // term would let a caller break out of the username prefix filter syntax (e.g.
                // search="x\" or attribute=\"y" would change the predicate). The whitelist
                // covers the characters legitimately used in Cognito usernames and emails.
                if (!ValidSearchPattern().IsMatch(search))
                {
                    LogInvalidSearchInput(search.Length);
                    return [];
                }

                // For broad search, filter on username prefix
                request.Filter = $"username ^= \"{search}\"";
            }

            if (max.HasValue)
            {
                request.Limit = max.Value;
            }

            ListUsersResponse response = await cognitoClient
                .ListUsersAsync(request, cancellationToken)
                .ConfigureAwait(false);

            return response.Users.ConvertAll(ToIdentityUser);
        }
        catch (NotAuthorizedException ex)
        {
            throw new IdentityProviderUnauthorizedException(ProviderName, "list_users", ex);
        }
        catch (AmazonServiceException ex) when (IsAuthorizationFailure(ex))
        {
            throw new IdentityProviderUnauthorizedException(ProviderName, "list_users", ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogListUsersFailed(ex);
            throw ClassifyReadFault(ex, "list_users");
        }
    }

    /// <inheritdoc/>
    public async Task<IIdentityUser?> GetUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityCognitoActivitySource.Source.StartActivity(
            IdentityCognitoActivitySource.Operations.GetUser);
        activity?.SetTag(IdentityCognitoActivitySource.Tags.UserId, userId);

        try
        {
            AdminGetUserRequest request = new()
            {
                UserPoolId = _options.UserPoolId,
                Username = userId,
            };

            AdminGetUserResponse response = await cognitoClient
                .AdminGetUserAsync(request, cancellationToken)
                .ConfigureAwait(false);

            return ToIdentityUser(response);
        }
        catch (UserNotFoundException)
        {
            return null;
        }
        catch (NotAuthorizedException ex)
        {
            throw new IdentityProviderUnauthorizedException(ProviderName, "get_user", ex);
        }
        catch (AmazonServiceException ex) when (IsAuthorizationFailure(ex))
        {
            throw new IdentityProviderUnauthorizedException(ProviderName, "get_user", ex);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogGetUserFailed(userId, ex);
            throw ClassifyReadFault(ex, "get_user");
        }
    }

    // ── IIdentityUserWriter ────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task SetUserEnabledAsync(
        string userId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        string operation = enabled
            ? IdentityCognitoActivitySource.Operations.EnableUser
            : IdentityCognitoActivitySource.Operations.DisableUser;

        using Activity? activity = IdentityCognitoActivitySource.Source.StartActivity(operation);
        activity?.SetTag(IdentityCognitoActivitySource.Tags.UserId, userId);

        if (enabled)
        {
            await cognitoClient.AdminEnableUserAsync(
                new AdminEnableUserRequest { UserPoolId = _options.UserPoolId, Username = userId },
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await cognitoClient.AdminDisableUserAsync(
                new AdminDisableUserRequest { UserPoolId = _options.UserPoolId, Username = userId },
                cancellationToken).ConfigureAwait(false);
        }

        await distributedEventBus.PublishAsync(
            new IdentityUserEnabledChangedEto(userId, enabled),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateUserAsync(
        string userId,
        IdentityUserUpdate update,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityCognitoActivitySource.Source.StartActivity(
            IdentityCognitoActivitySource.Operations.UpdateUser);
        activity?.SetTag(IdentityCognitoActivitySource.Tags.UserId, userId);

        List<AttributeType> attributes = [];

        if (update.Email is not null)
        {
            attributes.Add(new AttributeType { Name = EmailAttribute, Value = update.Email });
        }

        if (update.FirstName is not null)
        {
            attributes.Add(new AttributeType { Name = GivenNameAttribute, Value = update.FirstName });
        }

        if (update.LastName is not null)
        {
            attributes.Add(new AttributeType { Name = FamilyNameAttribute, Value = update.LastName });
        }

        if (update.Attributes is not null)
        {
            foreach (KeyValuePair<string, string?> attr in update.Attributes)
            {
                string attrName = attr.Key.StartsWith("custom:", StringComparison.Ordinal)
                    ? attr.Key
                    : $"custom:{attr.Key}";

                attributes.Add(new AttributeType { Name = attrName, Value = attr.Value ?? string.Empty });
            }
        }

        if (attributes.Count > 0)
        {
            AdminUpdateUserAttributesRequest request = new()
            {
                UserPoolId = _options.UserPoolId,
                Username = userId,
                UserAttributes = attributes,
            };

            await cognitoClient
                .AdminUpdateUserAttributesAsync(request, cancellationToken)
                .ConfigureAwait(false);
        }

        await distributedEventBus.PublishAsync(
            new IdentityUserProfileUpdatedEto(userId, update),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IIdentityUser> CreateUserAsync(
        IdentityUserCreate user,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityCognitoActivitySource.Source.StartActivity(
            IdentityCognitoActivitySource.Operations.CreateUser);

        AdminCreateUserRequest request = new()
        {
            UserPoolId = _options.UserPoolId,
            Username = user.Username,
            UserAttributes =
            [
                new AttributeType { Name = EmailAttribute, Value = user.Email },
                new AttributeType { Name = "email_verified", Value = "true" },
            ],
        };

        if (user.FirstName is not null)
        {
            request.UserAttributes.Add(new AttributeType { Name = GivenNameAttribute, Value = user.FirstName });
        }

        if (user.LastName is not null)
        {
            request.UserAttributes.Add(new AttributeType { Name = FamilyNameAttribute, Value = user.LastName });
        }

        if (user.TemporaryPassword is not null)
        {
            request.TemporaryPassword = user.TemporaryPassword;
        }

        AdminCreateUserResponse response = await cognitoClient
            .AdminCreateUserAsync(request, cancellationToken)
            .ConfigureAwait(false);

        FederatedIdentityUser createdUser = ToIdentityUser(response.User);

        if (!user.Enabled)
        {
            await SetUserEnabledAsync(createdUser.UserId, false, cancellationToken).ConfigureAwait(false);
        }

        await distributedEventBus.PublishAsync(
            new IdentityUserCreatedEto(createdUser.UserId, createdUser.Username ?? user.Username, createdUser.Email),
            cancellationToken).ConfigureAwait(false);

        return createdUser;
    }

    // ── IIdentityRoleManager ───────────────────────────────────────────────
    // Cognito has no native "roles" — groups serve this purpose.

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityRole>> GetRolesAsync(
        CancellationToken cancellationToken = default)
    {
        // Cognito groups serve as roles
        IReadOnlyList<IdentityGroup> groups = await GetGroupsAsync(cancellationToken).ConfigureAwait(false);
        return groups.Select(g => new IdentityRole(g.Id, g.Name, null)).ToList();
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IIdentityUser>> GetRoleMembersAsync(
        string roleName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            ListUsersInGroupRequest request = new()
            {
                UserPoolId = _options.UserPoolId,
                GroupName = roleName,
            };

            ListUsersInGroupResponse response = await cognitoClient
                .ListUsersInGroupAsync(request, cancellationToken)
                .ConfigureAwait(false);

            return response.Users.ConvertAll(ToIdentityUser);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogOperationFailed("GetRoleMembers", ex);
            throw ClassifyReadFault(ex, "list_role_members");
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityRole>> GetUserRolesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<IdentityGroup> groups = await GetUserGroupsAsync(userId, cancellationToken)
            .ConfigureAwait(false);
        return groups.Select(g => new IdentityRole(g.Id, g.Name, null)).ToList();
    }

    /// <inheritdoc/>
    public Task AssignRoleAsync(
        string userId,
        string roleName,
        CancellationToken cancellationToken = default) =>
        AddUserToGroupAsync(userId, roleName, cancellationToken);

    /// <inheritdoc/>
    public Task RemoveRoleAsync(
        string userId,
        string roleName,
        CancellationToken cancellationToken = default) =>
        RemoveUserFromGroupAsync(userId, roleName, cancellationToken);

    // ── IIdentityGroupManager ──────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityGroup>> GetGroupsAsync(
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityCognitoActivitySource.Source.StartActivity(
            IdentityCognitoActivitySource.Operations.ListGroups);

        try
        {
            ListGroupsRequest request = new() { UserPoolId = _options.UserPoolId };

            ListGroupsResponse response = await cognitoClient
                .ListGroupsAsync(request, cancellationToken)
                .ConfigureAwait(false);

            return response.Groups.ConvertAll(g => new IdentityGroup(
                g.GroupName, g.GroupName, null, []));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogOperationFailed("GetGroups", ex);
            throw ClassifyReadFault(ex, "list_groups");
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityGroup>> GetUserGroupsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityCognitoActivitySource.Source.StartActivity(
            IdentityCognitoActivitySource.Operations.ListGroupsForUser);
        activity?.SetTag(IdentityCognitoActivitySource.Tags.UserId, userId);

        try
        {
            AdminListGroupsForUserRequest request = new()
            {
                UserPoolId = _options.UserPoolId,
                Username = userId,
            };

            AdminListGroupsForUserResponse response = await cognitoClient
                .AdminListGroupsForUserAsync(request, cancellationToken)
                .ConfigureAwait(false);

            return response.Groups.ConvertAll(g => new IdentityGroup(
                g.GroupName, g.GroupName, null, []));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogOperationFailed("GetUserGroups", ex);
            throw ClassifyReadFault(ex, "list_user_groups");
        }
    }

    /// <inheritdoc/>
    public async Task AddUserToGroupAsync(
        string userId,
        string groupId,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityCognitoActivitySource.Source.StartActivity(
            IdentityCognitoActivitySource.Operations.AddUserToGroup);
        activity?.SetTag(IdentityCognitoActivitySource.Tags.UserId, userId);

        AdminAddUserToGroupRequest request = new()
        {
            UserPoolId = _options.UserPoolId,
            Username = userId,
            GroupName = groupId,
        };

        await cognitoClient
            .AdminAddUserToGroupAsync(request, cancellationToken)
            .ConfigureAwait(false);

        await distributedEventBus.PublishAsync(
            new IdentityGroupMembershipChangedEto(userId, groupId, true),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RemoveUserFromGroupAsync(
        string userId,
        string groupId,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityCognitoActivitySource.Source.StartActivity(
            IdentityCognitoActivitySource.Operations.RemoveUserFromGroup);
        activity?.SetTag(IdentityCognitoActivitySource.Tags.UserId, userId);

        AdminRemoveUserFromGroupRequest request = new()
        {
            UserPoolId = _options.UserPoolId,
            Username = userId,
            GroupName = groupId,
        };

        await cognitoClient
            .AdminRemoveUserFromGroupAsync(request, cancellationToken)
            .ConfigureAwait(false);

        await distributedEventBus.PublishAsync(
            new IdentityGroupMembershipChangedEto(userId, groupId, false),
            cancellationToken).ConfigureAwait(false);
    }

    // ── IUserSessionProvider / IUserDeviceProvider ─────────────────────────

    /// <inheritdoc/>
    /// <remarks>
    /// Cognito exposes no list-sessions API, so this reports no sessions.
    /// <see cref="IUserSessionManager"/> still resolves the canonical <c>/sessions</c> endpoint;
    /// it simply surfaces nothing for a Cognito backend.
    /// </remarks>
    Task<IReadOnlyList<UserSessionDescriptor>> IUserSessionProvider.ListAsync(
        string userId,
        string? currentSessionId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<UserSessionDescriptor>>([]);

    /// <inheritdoc/>
    /// <remarks>
    /// Cognito has no per-session revoke (only bulk <c>AdminUserGlobalSignOut</c>). The admin endpoint
    /// gates this on <see cref="IIdentityProviderCapabilities.SupportsIndividualSessionTermination"/>,
    /// which <see cref="CognitoIdentityProviderCapabilities"/> reports as <see langword="false"/>, so
    /// this is never reached in practice.
    /// </remarks>
    Task<bool> IUserSessionProvider.RevokeAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "AWS Cognito does not support individual session revocation — only global sign-out.");

    /// <inheritdoc/>
    /// <remarks>
    /// "Revoke others" requires excluding the current session, but Cognito's only bulk primitive
    /// (<c>AdminUserGlobalSignOut</c>) signs out every session indiscriminately and cannot spare the
    /// caller's. There is no per-session revoke to fall back on, so this is unsupported; the admin
    /// endpoint gates it via <see cref="IIdentityProviderCapabilities.SupportsIndividualSessionTermination"/>
    /// (reported <see langword="false"/>), so it is never reached in practice.
    /// </remarks>
    Task<int> IUserSessionProvider.RevokeOthersAsync(
        string userId,
        string currentSessionId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "AWS Cognito cannot revoke other sessions while sparing the current one — its only bulk " +
            "primitive (global sign-out) terminates every session, including the caller's.");

    /// <inheritdoc/>
    /// <remarks>
    /// Cognito device tracking is opt-in and does not map onto the canonical device shape, so this
    /// reports no devices.
    /// </remarks>
    Task<IReadOnlyList<UserDevice>> IUserDeviceProvider.ListAsync(
        string userId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<UserDevice>>([]);

    // ── IIdentityPasswordManager ───────────────────────────────────────────

    /// <inheritdoc/>
    public Task<DateTimeOffset?> GetPasswordChangedAtAsync(
        string userId,
        CancellationToken cancellationToken = default) =>
        // Cognito does not expose password change timestamp
        Task.FromResult<DateTimeOffset?>(null);

    /// <inheritdoc/>
    public async Task SendPasswordResetEmailAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityCognitoActivitySource.Source.StartActivity(
            IdentityCognitoActivitySource.Operations.ResetPassword);
        activity?.SetTag(IdentityCognitoActivitySource.Tags.UserId, userId);

        AdminResetUserPasswordRequest request = new()
        {
            UserPoolId = _options.UserPoolId,
            Username = userId,
        };

        await cognitoClient
            .AdminResetUserPasswordAsync(request, cancellationToken)
            .ConfigureAwait(false);

        await distributedEventBus.PublishAsync(
            new IdentityPasswordResetEto(userId),
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task SetTemporaryPasswordAsync(
        string userId,
        string temporaryPassword,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityCognitoActivitySource.Source.StartActivity(
            IdentityCognitoActivitySource.Operations.SetPassword);
        activity?.SetTag(IdentityCognitoActivitySource.Tags.UserId, userId);

        AdminSetUserPasswordRequest request = new()
        {
            UserPoolId = _options.UserPoolId,
            Username = userId,
            Password = temporaryPassword,
            Permanent = false,
        };

        await cognitoClient
            .AdminSetUserPasswordAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    // ── IIdentityCredentialVerifier ────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<bool> VerifyUserCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityCognitoActivitySource.Source.StartActivity(
            IdentityCognitoActivitySource.Operations.VerifyCredentials);

        if (string.IsNullOrEmpty(_options.AppClientId))
        {
            LogCredentialVerificationNotConfigured();
            return false;
        }

        try
        {
            AdminInitiateAuthRequest request = new()
            {
                UserPoolId = _options.UserPoolId,
                ClientId = _options.AppClientId,
                AuthFlow = AuthFlowType.ADMIN_USER_PASSWORD_AUTH,
                AuthParameters = new Dictionary<string, string>
                {
                    ["USERNAME"] = username,
                    ["PASSWORD"] = password,
                },
            };

            AdminInitiateAuthResponse response = await cognitoClient
                .AdminInitiateAuthAsync(request, cancellationToken)
                .ConfigureAwait(false);

            return response.AuthenticationResult is not null;
        }
        catch (NotAuthorizedException)
        {
            return false;
        }
        catch (UserNotFoundException)
        {
            return false;
        }
        catch (Exception ex)
        {
            LogCredentialVerificationFailed(ex);
            return false;
        }
    }

    // ── IIdentityClientRoleManager (Phase 2) ───────────────────────────────
    // Cognito groups are flat per User Pool with no native client binding. Granit
    // infers client scope from a naming prefix: a group named
    // "{appClientId}{Delimiter}{roleName}" is treated as a client role whose
    // ClientId is {appClientId}. Un-prefixed groups keep flowing through the
    // existing realm-role path — see ADR-027.

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetClientsAsync(
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityCognitoActivitySource.Source.StartActivity(
            IdentityCognitoActivitySource.Operations.ListUserPoolClients);

        try
        {
            List<string> clientIds = [];
            string? paginationToken = null;

            do
            {
                ListUserPoolClientsRequest request = new()
                {
                    UserPoolId = _options.UserPoolId,
                    MaxResults = 60,
                    NextToken = paginationToken,
                };

                ListUserPoolClientsResponse response = await cognitoClient
                    .ListUserPoolClientsAsync(request, cancellationToken)
                    .ConfigureAwait(false);

                clientIds.AddRange(response.UserPoolClients.Select(c => c.ClientId));
                paginationToken = response.NextToken;
            }
            while (!string.IsNullOrEmpty(paginationToken));

            return clientIds;
        }
        catch (Exception ex)
        {
            LogOperationFailed("ListUserPoolClients", ex);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityRole>> GetClientRolesAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        using Activity? activity = IdentityCognitoActivitySource.Source.StartActivity(
            IdentityCognitoActivitySource.Operations.GetClientRoles);
        activity?.SetTag(IdentityCognitoActivitySource.Tags.ClientId, clientId);

        string prefix = clientId + _clientRoleSyncOptions.Delimiter;

        try
        {
            List<IdentityRole> roles = [];
            string? paginationToken = null;

            do
            {
                ListGroupsRequest request = new()
                {
                    UserPoolId = _options.UserPoolId,
                    Limit = 60,
                    NextToken = paginationToken,
                };

                ListGroupsResponse response = await cognitoClient
                    .ListGroupsAsync(request, cancellationToken)
                    .ConfigureAwait(false);

                foreach (GroupType group in response.Groups)
                {
                    if (!group.GroupName.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string roleName = group.GroupName[prefix.Length..];
                    if (string.IsNullOrEmpty(roleName))
                    {
                        continue;
                    }

                    roles.Add(new IdentityRole(
                        Id: group.GroupName,
                        Name: roleName,
                        Description: group.Description)
                    { ClientId = clientId });
                }

                paginationToken = response.NextToken;
            }
            while (!string.IsNullOrEmpty(paginationToken));

            return roles;
        }
        catch (Exception ex)
        {
            LogOperationFailed("GetClientRoles", ex);
            return [];
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IdentityRole>> GetUserClientRolesAsync(
        string userId,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        using Activity? activity = IdentityCognitoActivitySource.Source.StartActivity(
            IdentityCognitoActivitySource.Operations.GetUserClientRoles);
        activity?.SetTag(IdentityCognitoActivitySource.Tags.UserId, userId);
        activity?.SetTag(IdentityCognitoActivitySource.Tags.ClientId, clientId);

        string prefix = clientId + _clientRoleSyncOptions.Delimiter;

        try
        {
            List<IdentityRole> roles = [];
            string? paginationToken = null;

            do
            {
                AdminListGroupsForUserRequest request = new()
                {
                    UserPoolId = _options.UserPoolId,
                    Username = userId,
                    Limit = 60,
                    NextToken = paginationToken,
                };

                AdminListGroupsForUserResponse response = await cognitoClient
                    .AdminListGroupsForUserAsync(request, cancellationToken)
                    .ConfigureAwait(false);

                foreach (GroupType group in response.Groups)
                {
                    if (!group.GroupName.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    string roleName = group.GroupName[prefix.Length..];
                    if (string.IsNullOrEmpty(roleName))
                    {
                        continue;
                    }

                    roles.Add(new IdentityRole(
                        Id: group.GroupName,
                        Name: roleName,
                        Description: group.Description)
                    { ClientId = clientId });
                }

                paginationToken = response.NextToken;
            }
            while (!string.IsNullOrEmpty(paginationToken));

            return roles;
        }
        catch (Exception ex)
        {
            LogOperationFailed("GetUserClientRoles", ex);
            return [];
        }
    }

    // ── Phase 3: Client-role writes (ADR-031) ──────────────────────────────

    /// <inheritdoc/>
    public async Task<IdentityRole> CreateClientRoleAsync(
        string clientId,
        string name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        using Activity? activity = IdentityCognitoActivitySource.Source.StartActivity(
            IdentityCognitoActivitySource.Operations.CreateClientRole);
        activity?.SetTag(IdentityCognitoActivitySource.Tags.ClientId, clientId);

        string groupName = clientId + _clientRoleSyncOptions.Delimiter + name;

        CreateGroupRequest request = new()
        {
            UserPoolId = _options.UserPoolId,
            GroupName = groupName,
            Description = description,
        };

        CreateGroupResponse response = await cognitoClient
            .CreateGroupAsync(request, cancellationToken)
            .ConfigureAwait(false);

        // Cognito "roles" are groups; the group name carries the Granit id semantics —
        // there is no separate id. Returning the group name as Id preserves the contract
        // that downstream code can round-trip the role.
        return new IdentityRole(
            Id: response.Group.GroupName,
            Name: name,
            Description: response.Group.Description)
        { ClientId = clientId };
    }

    /// <inheritdoc/>
    public async Task AssignClientRoleAsync(
        string userId,
        string clientId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        using Activity? activity = IdentityCognitoActivitySource.Source.StartActivity(
            IdentityCognitoActivitySource.Operations.AssignClientRole);
        activity?.SetTag(IdentityCognitoActivitySource.Tags.UserId, userId);
        activity?.SetTag(IdentityCognitoActivitySource.Tags.ClientId, clientId);

        string groupName = clientId + _clientRoleSyncOptions.Delimiter + roleName;

        AdminAddUserToGroupRequest request = new()
        {
            UserPoolId = _options.UserPoolId,
            Username = userId,
            GroupName = groupName,
        };

        await cognitoClient
            .AdminAddUserToGroupAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task RemoveClientRoleAsync(
        string userId,
        string clientId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        using Activity? activity = IdentityCognitoActivitySource.Source.StartActivity(
            IdentityCognitoActivitySource.Operations.RemoveClientRole);
        activity?.SetTag(IdentityCognitoActivitySource.Tags.UserId, userId);
        activity?.SetTag(IdentityCognitoActivitySource.Tags.ClientId, clientId);

        string groupName = clientId + _clientRoleSyncOptions.Delimiter + roleName;

        AdminRemoveUserFromGroupRequest request = new()
        {
            UserPoolId = _options.UserPoolId,
            Username = userId,
            GroupName = groupName,
        };

        await cognitoClient
            .AdminRemoveUserFromGroupAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    // ── Mapping helpers ────────────────────────────────────────────────────

    private static FederatedIdentityUser ToIdentityUser(UserType user)
    {
        var attributes = user.Attributes
            .ToDictionary(a => a.Name, a => a.Value);

        return new FederatedIdentityUser(
            UserId: user.Username,
            Username: user.Username,
            Email: attributes.GetValueOrDefault(EmailAttribute),
            FirstName: attributes.GetValueOrDefault(GivenNameAttribute),
            LastName: attributes.GetValueOrDefault(FamilyNameAttribute),
            Enabled: user.Enabled == true,
            Metadata: attributes.Where(a => a.Key.StartsWith("custom:", StringComparison.Ordinal))
                .ToDictionary(a => a.Key, a => a.Value));
    }

    private static FederatedIdentityUser ToIdentityUser(AdminGetUserResponse response)
    {
        var attributes = response.UserAttributes
            .ToDictionary(a => a.Name, a => a.Value);

        return new FederatedIdentityUser(
            UserId: response.Username,
            Username: response.Username,
            Email: attributes.GetValueOrDefault(EmailAttribute),
            FirstName: attributes.GetValueOrDefault(GivenNameAttribute),
            LastName: attributes.GetValueOrDefault(FamilyNameAttribute),
            Enabled: response.Enabled == true,
            Metadata: attributes.Where(a => a.Key.StartsWith("custom:", StringComparison.Ordinal))
                .ToDictionary(a => a.Key, a => a.Value));
    }

    // ── Source-generated logging ───────────────────────────────────────────

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to list Cognito users")]
    private partial void LogListUsersFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to get Cognito user {UserId}")]
    private partial void LogGetUserFailed(string userId, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cognito operation {Operation} failed")]
    private partial void LogOperationFailed(string operation, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Credential verification not configured: AppClientId is not set")]
    private partial void LogCredentialVerificationNotConfigured();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Credential verification failed")]
    private partial void LogCredentialVerificationFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rejected Cognito ListUsers search input ({Length} chars) — failed character whitelist")]
    private partial void LogInvalidSearchInput(int length);

    /// <summary>
    /// Whitelist for Cognito <c>ListUsers</c> search input. Limited to characters that
    /// legitimately appear in usernames and emails so a stray quote cannot break out of
    /// the <c>username ^= "…"</c> filter syntax. Bounded length to limit DoS surface.
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9._@+\-]{1,128}$")]
    private static partial Regex ValidSearchPattern();
}
