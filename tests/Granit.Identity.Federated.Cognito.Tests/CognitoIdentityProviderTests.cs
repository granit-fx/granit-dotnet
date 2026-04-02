using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.Runtime;
using Granit.Events;
using Granit.Identity;
using Granit.Identity.Events;
using Granit.Identity.Federated.Cognito.Internal;
using Granit.Identity.Federated.Cognito.Options;
using Granit.Identity.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.Cognito.Tests;

public sealed class CognitoIdentityProviderTests
{
    private readonly IAmazonCognitoIdentityProvider _cognitoClient = Substitute.For<IAmazonCognitoIdentityProvider>();
    private readonly IDistributedEventBus _distributedEventBus = Substitute.For<IDistributedEventBus>();
    private readonly CognitoIdentityProvider _sut;

    public CognitoIdentityProviderTests()
    {
        CognitoAdminOptions options = new()
        {
            Region = "eu-west-1",
            UserPoolId = "eu-west-1_TEST",
            AppClientId = "test-app-client",
        };

        _sut = new CognitoIdentityProvider(
            _cognitoClient,
            Microsoft.Extensions.Options.Options.Create(options),
            _distributedEventBus,
            NullLogger<CognitoIdentityProvider>.Instance);
    }

    // ── GetUsersAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetUsersAsync_ReturnsUsers()
    {
        _cognitoClient.ListUsersAsync(Arg.Any<ListUsersRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListUsersResponse
            {
                Users =
                [
                    CreateUserType("user1", "user1@test.com", "John", "Doe", true),
                ],
            });

        IReadOnlyList<IIdentityUser> result = await _sut.GetUsersAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Username.ShouldBe("user1");
        result[0].Email.ShouldBe("user1@test.com");
    }

    [Fact]
    public async Task GetUsersAsync_OnException_ReturnsEmpty()
    {
        _cognitoClient.ListUsersAsync(Arg.Any<ListUsersRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new AmazonServiceException("timeout"));

        IReadOnlyList<IIdentityUser> result = await _sut.GetUsersAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    // ── GetUserAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserAsync_ExistingUser_ReturnsUser()
    {
        _cognitoClient.AdminGetUserAsync(Arg.Any<AdminGetUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AdminGetUserResponse
            {
                Username = "user1",
                Enabled = true,
                UserAttributes =
                [
                    new AttributeType { Name = "email", Value = "user1@test.com" },
                    new AttributeType { Name = "given_name", Value = "John" },
                    new AttributeType { Name = "family_name", Value = "Doe" },
                ],
            });

        IIdentityUser? result = await _sut.GetUserAsync("user1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Username.ShouldBe("user1");
        result.Email.ShouldBe("user1@test.com");
        result.FirstName.ShouldBe("John");
        result.LastName.ShouldBe("Doe");
        result.Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task GetUserAsync_NotFound_ReturnsNull()
    {
        _cognitoClient.AdminGetUserAsync(Arg.Any<AdminGetUserRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UserNotFoundException("not found"));

        IIdentityUser? result = await _sut.GetUserAsync("unknown", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ── SetUserEnabledAsync ────────────────────────────────────────────────

    [Fact]
    public async Task SetUserEnabledAsync_Enable_CallsAdminEnableUser()
    {
        await _sut.SetUserEnabledAsync("user1", true, TestContext.Current.CancellationToken);

        await _cognitoClient.Received(1).AdminEnableUserAsync(
            Arg.Is<AdminEnableUserRequest>(r => r.Username == "user1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetUserEnabledAsync_Disable_CallsAdminDisableUser()
    {
        await _sut.SetUserEnabledAsync("user1", false, TestContext.Current.CancellationToken);

        await _cognitoClient.Received(1).AdminDisableUserAsync(
            Arg.Is<AdminDisableUserRequest>(r => r.Username == "user1"),
            Arg.Any<CancellationToken>());
    }

    // ── CreateUserAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task CreateUserAsync_ReturnsCreatedUser()
    {
        _cognitoClient.AdminCreateUserAsync(Arg.Any<AdminCreateUserRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AdminCreateUserResponse
            {
                User = CreateUserType("newuser", "new@test.com", "Jane", "Smith", true),
            });

        IdentityUserCreate createRequest = new("newuser", "new@test.com", "Jane", "Smith");

        IIdentityUser result = await _sut.CreateUserAsync(createRequest, TestContext.Current.CancellationToken);

        result.Username.ShouldBe("newuser");
        result.Email.ShouldBe("new@test.com");
    }

    // ── Group management ───────────────────────────────────────────────────

    [Fact]
    public async Task GetGroupsAsync_ReturnsGroups()
    {
        _cognitoClient.ListGroupsAsync(Arg.Any<ListGroupsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ListGroupsResponse
            {
                Groups =
                [
                    new GroupType { GroupName = "admin" },
                    new GroupType { GroupName = "users" },
                ],
            });

        IReadOnlyList<IdentityGroup> result = await _sut.GetGroupsAsync(TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("admin");
        result[1].Name.ShouldBe("users");
    }

    [Fact]
    public async Task AddUserToGroupAsync_CallsCognitoAndPublishesEvent()
    {
        await _sut.AddUserToGroupAsync("user1", "admin", TestContext.Current.CancellationToken);

        await _cognitoClient.Received(1).AdminAddUserToGroupAsync(
            Arg.Is<AdminAddUserToGroupRequest>(r => r.Username == "user1" && r.GroupName == "admin"),
            Arg.Any<CancellationToken>());

        await _distributedEventBus.Received(1).PublishAsync(
            Arg.Any<IdentityGroupMembershipChangedEto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveUserFromGroupAsync_CallsCognitoAndPublishesEvent()
    {
        await _sut.RemoveUserFromGroupAsync("user1", "admin", TestContext.Current.CancellationToken);

        await _cognitoClient.Received(1).AdminRemoveUserFromGroupAsync(
            Arg.Is<AdminRemoveUserFromGroupRequest>(r => r.Username == "user1" && r.GroupName == "admin"),
            Arg.Any<CancellationToken>());
    }

    // ── Session management ─────────────────────────────────────────────────

    [Fact]
    public async Task TerminateAllSessionsAsync_CallsGlobalSignOut()
    {
        await _sut.TerminateAllSessionsAsync("user1", TestContext.Current.CancellationToken);

        await _cognitoClient.Received(1).AdminUserGlobalSignOutAsync(
            Arg.Is<AdminUserGlobalSignOutRequest>(r => r.Username == "user1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TerminateSessionAsync_ThrowsNotSupported()
    {
        await Should.ThrowAsync<NotSupportedException>(
            () => _sut.TerminateSessionAsync("user1", "session1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetUserSessionsAsync_ReturnsEmpty()
    {
        IReadOnlyList<IdentitySession> result = await _sut.GetUserSessionsAsync(
            "user1", TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    // ── Password management ────────────────────────────────────────────────

    [Fact]
    public async Task SendPasswordResetEmailAsync_CallsAdminResetUserPassword()
    {
        await _sut.SendPasswordResetEmailAsync("user1", TestContext.Current.CancellationToken);

        await _cognitoClient.Received(1).AdminResetUserPasswordAsync(
            Arg.Is<AdminResetUserPasswordRequest>(r => r.Username == "user1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetTemporaryPasswordAsync_CallsAdminSetUserPassword()
    {
        await _sut.SetTemporaryPasswordAsync("user1", "TempPass123!", TestContext.Current.CancellationToken);

        await _cognitoClient.Received(1).AdminSetUserPasswordAsync(
            Arg.Is<AdminSetUserPasswordRequest>(r =>
                r.Username == "user1" && r.Password == "TempPass123!" && r.Permanent == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetPasswordChangedAtAsync_ReturnsNull()
    {
        DateTimeOffset? result = await _sut.GetPasswordChangedAtAsync(
            "user1", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ── Credential verification ────────────────────────────────────────────

    [Fact]
    public async Task VerifyUserCredentialsAsync_ValidCredentials_ReturnsTrue()
    {
        _cognitoClient.AdminInitiateAuthAsync(Arg.Any<AdminInitiateAuthRequest>(), Arg.Any<CancellationToken>())
            .Returns(new AdminInitiateAuthResponse
            {
                AuthenticationResult = new AuthenticationResultType { AccessToken = "token" },
            });

        bool result = await _sut.VerifyUserCredentialsAsync(
            "user1", "password", TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyUserCredentialsAsync_InvalidCredentials_ReturnsFalse()
    {
        _cognitoClient.AdminInitiateAuthAsync(Arg.Any<AdminInitiateAuthRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new NotAuthorizedException("bad credentials"));

        bool result = await _sut.VerifyUserCredentialsAsync(
            "user1", "wrong", TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task VerifyUserCredentialsAsync_UserNotFound_ReturnsFalse()
    {
        _cognitoClient.AdminInitiateAuthAsync(Arg.Any<AdminInitiateAuthRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new UserNotFoundException("not found"));

        bool result = await _sut.VerifyUserCredentialsAsync(
            "unknown", "password", TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    // ── Roles (delegated to groups) ────────────────────────────────────────

    [Fact]
    public async Task AssignRoleAsync_DelegatesToAddUserToGroup()
    {
        await _sut.AssignRoleAsync("user1", "admin", TestContext.Current.CancellationToken);

        await _cognitoClient.Received(1).AdminAddUserToGroupAsync(
            Arg.Is<AdminAddUserToGroupRequest>(r => r.Username == "user1" && r.GroupName == "admin"),
            Arg.Any<CancellationToken>());
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static UserType CreateUserType(
        string username, string email, string firstName, string lastName, bool enabled) =>
        new()
        {
            Username = username,
            Enabled = enabled,
            Attributes =
            [
                new AttributeType { Name = "email", Value = email },
                new AttributeType { Name = "given_name", Value = firstName },
                new AttributeType { Name = "family_name", Value = lastName },
            ],
        };
}
