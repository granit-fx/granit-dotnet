using Granit.Identity.Internal;
using Granit.Identity.Models;
using Shouldly;
using Xunit;

namespace Granit.Identity.Tests.Internal;

public sealed class NullIdentityProviderTests
{
    private readonly NullIdentityProvider _provider = new();

    // -------------------------------------------------------------------------
    // User queries
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetUsersAsync_ReturnsEmptyList()
    {
        IReadOnlyList<IIdentityUser> result = await _provider.GetUsersAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetUsersAsync_WithSearch_ReturnsEmptyList()
    {
        IReadOnlyList<IIdentityUser> result = await _provider.GetUsersAsync(
            search: "alice", first: 0, max: 10,
            cancellationToken: TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetUserAsync_ReturnsNull()
    {
        IIdentityUser? result = await _provider.GetUserAsync(
            "any-user-id", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetPasswordChangedAtAsync_ReturnsNull()
    {
        DateTimeOffset? result = await _provider.GetPasswordChangedAtAsync(
            "any-user-id", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // User mutations
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SetUserEnabledAsync_CompletesWithoutError()
    {
        Func<Task> act = () => _provider.SetUserEnabledAsync(
            "user-1", true, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task UpdateUserAsync_CompletesWithoutError()
    {
        IdentityUserUpdate update = new("new@test.com", "Alice", "Doe");

        Func<Task> act = () => _provider.UpdateUserAsync(
            "user-1", update, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task CreateUserAsync_ReturnsNonNullUser()
    {
        IdentityUserCreate create = new("alice", "alice@test.com", "Alice", "Doe");

        IIdentityUser result = await _provider.CreateUserAsync(
            create, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Username.ShouldBe("alice");
        result.Email.ShouldBe("alice@test.com");
        result.FirstName.ShouldBe("Alice");
        result.LastName.ShouldBe("Doe");
        result.Enabled.ShouldBeTrue();
        result.UserId.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task CreateUserAsync_WithDisabledUser_ReturnsDisabledUser()
    {
        IdentityUserCreate create = new("bob", "bob@test.com", Enabled: false);

        IIdentityUser result = await _provider.CreateUserAsync(
            create, TestContext.Current.CancellationToken);

        result.Enabled.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Roles
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetRolesAsync_ReturnsEmptyList()
    {
        IReadOnlyList<IdentityRole> result = await _provider.GetRolesAsync(
            TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetRoleMembersAsync_ReturnsEmptyList()
    {
        IReadOnlyList<IIdentityUser> result = await _provider.GetRoleMembersAsync(
            "admin", TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetUserRolesAsync_ReturnsEmptyList()
    {
        IReadOnlyList<IdentityRole> result = await _provider.GetUserRolesAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task AssignRoleAsync_CompletesWithoutError()
    {
        Func<Task> act = () => _provider.AssignRoleAsync(
            "user-1", "admin", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task RemoveRoleAsync_CompletesWithoutError()
    {
        Func<Task> act = () => _provider.RemoveRoleAsync(
            "user-1", "admin", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    // -------------------------------------------------------------------------
    // Groups
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetGroupsAsync_ReturnsEmptyList()
    {
        IReadOnlyList<IdentityGroup> result = await _provider.GetGroupsAsync(
            TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetUserGroupsAsync_ReturnsEmptyList()
    {
        IReadOnlyList<IdentityGroup> result = await _provider.GetUserGroupsAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddUserToGroupAsync_CompletesWithoutError()
    {
        Func<Task> act = () => _provider.AddUserToGroupAsync(
            "user-1", "group-1", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task RemoveUserFromGroupAsync_CompletesWithoutError()
    {
        Func<Task> act = () => _provider.RemoveUserFromGroupAsync(
            "user-1", "group-1", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    // -------------------------------------------------------------------------
    // Password & credentials
    // -------------------------------------------------------------------------

    [Fact]
    public async Task SendPasswordResetEmailAsync_CompletesWithoutError()
    {
        Func<Task> act = () => _provider.SendPasswordResetEmailAsync(
            "user-1", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task SetTemporaryPasswordAsync_CompletesWithoutError()
    {
        Func<Task> act = () => _provider.SetTemporaryPasswordAsync(
            "user-1", "Temp123!", TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task VerifyUserCredentialsAsync_ReturnsFalse()
    {
        bool result = await _provider.VerifyUserCredentialsAsync(
            "alice", "password", TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Interface contract
    // -------------------------------------------------------------------------

    [Fact]
    public void ImplementsIIdentityProvider() =>
        _provider.ShouldBeAssignableTo<IIdentityProvider>();
}
