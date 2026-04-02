using System.Diagnostics;
using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Granit.Events;
using Granit.Identity.Events;
using Granit.Identity.Federated;
using Granit.Identity.Federated.Cognito.Diagnostics;
using Granit.Identity.Federated.Cognito.Options;
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
    IDistributedEventBus distributedEventBus,
    ILogger<CognitoIdentityProvider> logger) : IIdentityProvider
{
    private const string EmailAttribute = "email";
    private const string GivenNameAttribute = "given_name";
    private const string FamilyNameAttribute = "family_name";

    private readonly CognitoAdminOptions _options = options.Value;

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

            return response.Users.Select(ToIdentityUser).ToList();
        }
        catch (Exception ex)
        {
            LogListUsersFailed(ex);
            return [];
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
        catch (Exception ex)
        {
            LogGetUserFailed(userId, ex);
            return null;
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

            return response.Users.Select(ToIdentityUser).ToList();
        }
        catch (Exception ex)
        {
            LogOperationFailed("GetRoleMembers", ex);
            return [];
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

            return response.Groups.Select(g => new IdentityGroup(
                g.GroupName, g.GroupName, null, [])).ToList();
        }
        catch (Exception ex)
        {
            LogOperationFailed("GetGroups", ex);
            return [];
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

            return response.Groups.Select(g => new IdentityGroup(
                g.GroupName, g.GroupName, null, [])).ToList();
        }
        catch (Exception ex)
        {
            LogOperationFailed("GetUserGroups", ex);
            return [];
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

    // ── IIdentitySessionManager ────────────────────────────────────────────

    /// <inheritdoc/>
    public Task<IReadOnlyList<IdentitySession>> GetUserSessionsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Cognito does not expose a list-sessions API
        IReadOnlyList<IdentitySession> empty = [];
        return Task.FromResult(empty);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<IdentityDeviceActivity>> GetUserDeviceActivityAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        // Cognito device tracking is opt-in and does not map well to IdentityDeviceActivity
        IReadOnlyList<IdentityDeviceActivity> empty = [];
        return Task.FromResult(empty);
    }

    /// <inheritdoc/>
    public Task TerminateSessionAsync(
        string userId,
        string sessionId,
        CancellationToken cancellationToken = default) =>
        // Cognito does not support individual session termination
        throw new NotSupportedException("AWS Cognito does not support individual session termination. Use TerminateAllSessionsAsync instead.");

    /// <inheritdoc/>
    public async Task TerminateAllSessionsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = IdentityCognitoActivitySource.Source.StartActivity(
            IdentityCognitoActivitySource.Operations.GlobalSignOut);
        activity?.SetTag(IdentityCognitoActivitySource.Tags.UserId, userId);

        AdminUserGlobalSignOutRequest request = new()
        {
            UserPoolId = _options.UserPoolId,
            Username = userId,
        };

        await cognitoClient
            .AdminUserGlobalSignOutAsync(request, cancellationToken)
            .ConfigureAwait(false);

        await distributedEventBus.PublishAsync(
            new IdentitySessionsRevokedEto(userId),
            cancellationToken).ConfigureAwait(false);
    }

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
            ExtraProperties: attributes.Where(a => a.Key.StartsWith("custom:", StringComparison.Ordinal))
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
            ExtraProperties: attributes.Where(a => a.Key.StartsWith("custom:", StringComparison.Ordinal))
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
}
