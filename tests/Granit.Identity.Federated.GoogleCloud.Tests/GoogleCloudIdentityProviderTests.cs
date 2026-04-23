using System.Runtime.CompilerServices;
using FirebaseAdmin.Auth;
using Granit.Events;
using Granit.Identity;
using Granit.Identity.Events;
using Granit.Identity.Federated.GoogleCloud.Internal;
using Granit.Identity.Federated.GoogleCloud.Options;
using Granit.Identity.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.GoogleCloud.Tests;

public sealed class GoogleCloudIdentityProviderTests
{
    private readonly IFirebaseAuthTransport _transport = Substitute.For<IFirebaseAuthTransport>();
    private readonly IDistributedEventBus _distributedEventBus = Substitute.For<IDistributedEventBus>();
    private readonly GoogleCloudIdentityOptions _options = new() { ProjectId = "test-project", RolesClaimKey = "roles" };
    private readonly GoogleCloudIdentityProvider _sut;

    public GoogleCloudIdentityProviderTests()
    {
        _sut = new GoogleCloudIdentityProvider(
            _transport,
            Microsoft.Extensions.Options.Options.Create(_options),
            _distributedEventBus,
            NullLogger<GoogleCloudIdentityProvider>.Instance);
    }

    // ── GetUserAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserAsync_ReturnsUser_WhenFound()
    {
        UserRecord user = CreateUserRecord("uid-1", "john@example.com", "John Doe");
        _transport.GetUserAsync("uid-1", Arg.Any<CancellationToken>()).Returns(user);

        IIdentityUser? result = await _sut.GetUserAsync("uid-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.UserId.ShouldBe("uid-1");
        result.Email.ShouldBe("john@example.com");
        result.FirstName.ShouldBe("John");
        result.LastName.ShouldBe("Doe");
    }

    [Fact]
    public async Task GetUserAsync_ReturnsNull_WhenExceptionThrown()
    {
        _transport.GetUserAsync("uid-1", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("transport error"));

        IIdentityUser? result = await _sut.GetUserAsync("uid-1", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ── SetUserEnabledAsync ─────────────────────────────────────────────

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetUserEnabledAsync_UpdatesUserAndPublishesEvent(bool enabled)
    {
        await _sut.SetUserEnabledAsync("uid-1", enabled, TestContext.Current.CancellationToken);

        await _transport.Received(1).UpdateUserAsync(
            Arg.Is<UserRecordArgs>(a => a.Uid == "uid-1" && a.Disabled == !enabled),
            Arg.Any<CancellationToken>());
        await _distributedEventBus.Received(1).PublishAsync(
            Arg.Is<IdentityUserEnabledChangedEto>(e => e.UserId == "uid-1" && e.Enabled == enabled),
            Arg.Any<CancellationToken>());
    }

    // ── UpdateUserAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task UpdateUserAsync_UpdatesEmailAndDisplayName()
    {
        IdentityUserUpdate update = new(Email: "new@example.com", FirstName: "Jane", LastName: "Smith");

        await _sut.UpdateUserAsync("uid-1", update, TestContext.Current.CancellationToken);

        await _transport.Received(1).UpdateUserAsync(
            Arg.Is<UserRecordArgs>(a => a.Uid == "uid-1" && a.Email == "new@example.com" && a.DisplayName == "Jane Smith"),
            Arg.Any<CancellationToken>());
        await _distributedEventBus.Received(1).PublishAsync(
            Arg.Is<IdentityUserProfileUpdatedEto>(e => e.UserId == "uid-1"),
            Arg.Any<CancellationToken>());
    }

    // ── CreateUserAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateUserAsync_CreatesUserAndPublishesEvent()
    {
        IdentityUserCreate create = new(
            Username: "newuser",
            Email: "new@example.com",
            FirstName: "New",
            LastName: "User",
            Enabled: true,
            TemporaryPassword: "TempPass123!");

        UserRecord createdRecord = CreateUserRecord("new-uid", "new@example.com", "New User");
        _transport.CreateUserAsync(Arg.Any<UserRecordArgs>(), Arg.Any<CancellationToken>()).Returns(createdRecord);

        IIdentityUser result = await _sut.CreateUserAsync(create, TestContext.Current.CancellationToken);

        result.UserId.ShouldBe("new-uid");
        result.Email.ShouldBe("new@example.com");
        await _transport.Received(1).CreateUserAsync(
            Arg.Is<UserRecordArgs>(a =>
                a.Email == "new@example.com" &&
                a.DisplayName == "New User" &&
                a.Disabled == false &&
                a.Password == "TempPass123!"),
            Arg.Any<CancellationToken>());
        await _distributedEventBus.Received(1).PublishAsync(
            Arg.Is<IdentityUserCreatedEto>(e => e.UserId == "new-uid"),
            Arg.Any<CancellationToken>());
    }

    // ── Roles (custom claims) ───────────────────────────────────────────

    [Fact]
    public async Task GetRolesAsync_ReturnsEmpty()
    {
        IReadOnlyList<IdentityRole> roles = await _sut.GetRolesAsync(TestContext.Current.CancellationToken);

        roles.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetUserRolesAsync_ExtractsRolesFromCustomClaims()
    {
        UserRecord user = CreateUserRecordWithClaims("uid-1", "john@example.com", "John",
            new Dictionary<string, object> { ["roles"] = new List<object> { "admin", "editor" } });
        _transport.GetUserAsync("uid-1", Arg.Any<CancellationToken>()).Returns(user);

        IReadOnlyList<IdentityRole> roles = await _sut.GetUserRolesAsync("uid-1", TestContext.Current.CancellationToken);

        roles.Select(r => r.Name).ShouldContain("admin");
        roles.Select(r => r.Name).ShouldContain("editor");
    }

    [Fact]
    public async Task GetUserRolesAsync_ReturnsEmpty_WhenNoClaims()
    {
        UserRecord user = CreateUserRecord("uid-1", "john@example.com", "John");
        _transport.GetUserAsync("uid-1", Arg.Any<CancellationToken>()).Returns(user);

        IReadOnlyList<IdentityRole> roles = await _sut.GetUserRolesAsync("uid-1", TestContext.Current.CancellationToken);

        roles.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetUserRolesAsync_ReturnsEmpty_OnException()
    {
        _transport.GetUserAsync("uid-1", Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("error"));

        IReadOnlyList<IdentityRole> roles = await _sut.GetUserRolesAsync("uid-1", TestContext.Current.CancellationToken);

        roles.ShouldBeEmpty();
    }

    [Fact]
    public async Task AssignRoleAsync_AddsRoleAndPublishesEvent()
    {
        UserRecord user = CreateUserRecordWithClaims("uid-1", "john@example.com", "John",
            new Dictionary<string, object> { ["roles"] = new List<object> { "editor" } });
        _transport.GetUserAsync("uid-1", Arg.Any<CancellationToken>()).Returns(user);

        await _sut.AssignRoleAsync("uid-1", "admin", TestContext.Current.CancellationToken);

        await _transport.Received(1).SetCustomUserClaimsAsync(
            "uid-1",
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Any<CancellationToken>());
        await _distributedEventBus.Received(1).PublishAsync(
            Arg.Is<IdentityRoleAssignedEto>(e => e.UserId == "uid-1" && e.RoleName == "admin"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveRoleAsync_RemovesRoleAndPublishesEvent()
    {
        UserRecord user = CreateUserRecordWithClaims("uid-1", "john@example.com", "John",
            new Dictionary<string, object> { ["roles"] = new List<object> { "admin", "editor" } });
        _transport.GetUserAsync("uid-1", Arg.Any<CancellationToken>()).Returns(user);

        await _sut.RemoveRoleAsync("uid-1", "admin", TestContext.Current.CancellationToken);

        await _transport.Received(1).SetCustomUserClaimsAsync(
            "uid-1",
            Arg.Any<IReadOnlyDictionary<string, object>>(),
            Arg.Any<CancellationToken>());
        await _distributedEventBus.Received(1).PublishAsync(
            Arg.Is<IdentityRoleRemovedEto>(e => e.UserId == "uid-1" && e.RoleName == "admin"),
            Arg.Any<CancellationToken>());
    }

    // ── Groups (not supported) ──────────────────────────────────────────

    [Fact]
    public async Task GetGroupsAsync_ReturnsEmpty()
    {
        IReadOnlyList<IdentityGroup> groups = await _sut.GetGroupsAsync(TestContext.Current.CancellationToken);

        groups.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddUserToGroupAsync_ThrowsNotSupported()
    {
        await Should.ThrowAsync<NotSupportedException>(
            () => _sut.AddUserToGroupAsync("uid-1", "group", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveUserFromGroupAsync_ThrowsNotSupported()
    {
        await Should.ThrowAsync<NotSupportedException>(
            () => _sut.RemoveUserFromGroupAsync("uid-1", "group", TestContext.Current.CancellationToken));
    }

    // ── Sessions ────────────────────────────────────────────────────────

    [Fact]
    public async Task TerminateSessionAsync_ThrowsNotSupported()
    {
        await Should.ThrowAsync<NotSupportedException>(
            () => _sut.TerminateSessionAsync("uid-1", "session-1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TerminateAllSessionsAsync_RevokesTokensAndPublishesEvent()
    {
        await _sut.TerminateAllSessionsAsync("uid-1", TestContext.Current.CancellationToken);

        await _transport.Received(1).RevokeRefreshTokensAsync("uid-1", Arg.Any<CancellationToken>());
        await _distributedEventBus.Received(1).PublishAsync(
            Arg.Is<IdentitySessionsRevokedEto>(e => e.UserId == "uid-1"),
            Arg.Any<CancellationToken>());
    }

    // ── Password ────────────────────────────────────────────────────────

    [Fact]
    public async Task SendPasswordResetEmailAsync_GeneratesResetLink()
    {
        UserRecord user = CreateUserRecord("uid-1", "john@example.com", "John Doe");
        _transport.GetUserAsync("uid-1", Arg.Any<CancellationToken>()).Returns(user);
        _transport.GeneratePasswordResetLinkAsync("john@example.com", Arg.Any<CancellationToken>())
            .Returns("https://reset-link");

        await _sut.SendPasswordResetEmailAsync("uid-1", TestContext.Current.CancellationToken);

        await _transport.Received(1).GeneratePasswordResetLinkAsync("john@example.com", Arg.Any<CancellationToken>());
        await _distributedEventBus.Received(1).PublishAsync(
            Arg.Is<IdentityPasswordResetEto>(e => e.UserId == "uid-1"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SetTemporaryPasswordAsync_UpdatesPassword()
    {
        await _sut.SetTemporaryPasswordAsync("uid-1", "NewPass123!", TestContext.Current.CancellationToken);

        await _transport.Received(1).UpdateUserAsync(
            Arg.Is<UserRecordArgs>(a => a.Uid == "uid-1" && a.Password == "NewPass123!"),
            Arg.Any<CancellationToken>());
    }

    // ── Credentials ─────────────────────────────────────────────────────

    [Fact]
    public async Task VerifyUserCredentialsAsync_DelegatesToTransport()
    {
        _transport.VerifyPasswordAsync("john@example.com", "pass", Arg.Any<CancellationToken>())
            .Returns(true);

        bool result = await _sut.VerifyUserCredentialsAsync("john@example.com", "pass", TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static UserRecord CreateUserRecord(string uid, string? email, string? displayName,
        IReadOnlyDictionary<string, object>? customClaims = null)
    {
        var user = (UserRecord)RuntimeHelpers.GetUninitializedObject(typeof(UserRecord));
        SetProperty(user, "Uid", uid);
        SetProperty(user, "Email", email);
        SetProperty(user, "DisplayName", displayName);
        SetProperty(user, "Disabled", false);
        SetProperty(user, "CustomClaims", customClaims);
        return user;
    }

    private static UserRecord CreateUserRecordWithClaims(string uid, string? email, string? displayName,
        Dictionary<string, object> customClaims) =>
        CreateUserRecord(uid, email, displayName, customClaims);

    // ── GetUsersAsync (bounded pagination, no in-memory filter) ───────────────

    [Fact]
    public async Task GetUsersAsync_WithoutSearch_DelegatesPaginationToTransport()
    {
        // Previous implementation pulled every Firebase user into memory.
        // Provider must now forward the (first, max) window verbatim to the transport
        // so the bounded-page implementation can stop after the requested slice.
        _transport.ListUsersAsync(0, 25, Arg.Any<CancellationToken>())
            .Returns([]);

        IReadOnlyList<IIdentityUser> users = await _sut.GetUsersAsync(
            search: null, first: 0, max: 25, TestContext.Current.CancellationToken);

        users.ShouldBeEmpty();
        await _transport.Received(1).ListUsersAsync(0, 25, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetUsersAsync_WithSearch_ThrowsNotSupported()
    {
        // Previously we materialised every user in memory and filtered by
        // Email/DisplayName.Contains. Now refused — Firebase Admin SDK has no
        // server-side search filter on ListUsers.
        await Should.ThrowAsync<NotSupportedException>(async () =>
            await _sut.GetUsersAsync(
                search: "alice", first: null, max: null, TestContext.Current.CancellationToken));

        await _transport.DidNotReceive().ListUsersAsync(
            Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>());
    }

    private static void SetProperty(object target, string propertyName, object? value)
    {
        System.Reflection.PropertyInfo? prop = target.GetType().GetProperty(propertyName);
        if (prop is { CanWrite: true })
        {
            prop.SetValue(target, value);
            return;
        }

        // Try backing field (auto-property pattern: <PropertyName>k__BackingField)
        System.Reflection.FieldInfo? field = target.GetType().GetField($"<{propertyName}>k__BackingField",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(target, value);
    }
}
