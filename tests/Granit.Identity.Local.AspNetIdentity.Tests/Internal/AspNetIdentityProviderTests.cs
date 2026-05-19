using Granit.Identity.Local.AspNetIdentity.Internal;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Services;
using Granit.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Shouldly;
using Xunit;
using GranitIdentityGroup = Granit.Identity.Models.IdentityGroup;
using GranitIdentityRole = Granit.Identity.Models.IdentityRole;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Internal;

/// <summary>
/// Unit tests for <see cref="AspNetIdentityProvider"/>.
/// </summary>
public sealed class AspNetIdentityProviderTests
{
    private readonly UserManager<LocalIdentity> _userManager;
    private readonly RoleManager<GranitRole> _roleManager;
    private readonly ILocalIdentityGroupStore _groupStore;
    private readonly ILogger<AspNetIdentityProvider> _logger;
    private readonly AspNetIdentityProvider _sut;

    public AspNetIdentityProviderTests()
    {
        IUserStore<LocalIdentity> userStore = Substitute.For<IUserStore<LocalIdentity>>();
        _userManager = Substitute.For<UserManager<LocalIdentity>>(
            userStore, null, null, null, null, null, null, null, null);

        IRoleStore<GranitRole> roleStore = Substitute.For<IRoleStore<GranitRole>>();
        _roleManager = Substitute.For<RoleManager<GranitRole>>(
            roleStore, null, null, null, null);

        _groupStore = Substitute.For<ILocalIdentityGroupStore>();
        _logger = NullLogger<AspNetIdentityProvider>.Instance;

        _sut = new AspNetIdentityProvider(_userManager, _roleManager, _groupStore, _logger);
    }

    // ──── IIdentityUserReader ────

    [Fact]
    public async Task GetUserAsync_WhenUserExists_ReturnsUser()
    {
        LocalIdentity user = new() { UserName = "alice" };
        _userManager.FindByIdAsync("user-1").Returns(user);

        IIdentityUser? result = await _sut.GetUserAsync("user-1", TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(user);
    }

    [Fact]
    public async Task GetUserAsync_WhenUserNotFound_ReturnsNull()
    {
        _userManager.FindByIdAsync("missing").ReturnsNull();

        IIdentityUser? result = await _sut.GetUserAsync("missing", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    // ──── IIdentityUserWriter — CreateUserAsync ────

    [Fact]
    public async Task CreateUserAsync_WithoutPassword_CallsCreateWithoutPassword()
    {
        IdentityUserCreate input = new("alice", "alice@test.com", "Alice", "Smith", true, null);
        _userManager.CreateAsync(Arg.Any<LocalIdentity>()).Returns(IdentityResult.Success);

        IIdentityUser result = await _sut.CreateUserAsync(input, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        await _userManager.Received(1).CreateAsync(Arg.Is<LocalIdentity>(u =>
            u.UserName == "alice" &&
            u.Email == "alice@test.com" &&
            u.FirstName == "Alice" &&
            u.LastName == "Smith"));
        await _userManager.DidNotReceive().CreateAsync(Arg.Any<LocalIdentity>(), Arg.Any<string>());
    }

    [Fact]
    public async Task CreateUserAsync_WithEmptyPassword_CallsCreateWithoutPassword()
    {
        IdentityUserCreate input = new("bob", "bob@test.com", "Bob", "Jones", true, string.Empty);
        _userManager.CreateAsync(Arg.Any<LocalIdentity>()).Returns(IdentityResult.Success);

        await _sut.CreateUserAsync(input, TestContext.Current.CancellationToken);

        await _userManager.Received(1).CreateAsync(Arg.Any<LocalIdentity>());
        await _userManager.DidNotReceive().CreateAsync(Arg.Any<LocalIdentity>(), Arg.Any<string>());
    }

    [Fact]
    public async Task CreateUserAsync_WithTemporaryPassword_CallsCreateWithPassword()
    {
        IdentityUserCreate input = new("carol", "carol@test.com", "Carol", "Doe", true, "Temp123!");
        _userManager.CreateAsync(Arg.Any<LocalIdentity>(), "Temp123!").Returns(IdentityResult.Success);

        await _sut.CreateUserAsync(input, TestContext.Current.CancellationToken);

        await _userManager.Received(1).CreateAsync(Arg.Any<LocalIdentity>(), "Temp123!");
    }

    [Fact]
    public async Task CreateUserAsync_WhenDisabled_SetsLockoutToMaxValue()
    {
        IdentityUserCreate input = new("dave", "dave@test.com", "Dave", "Lee", false, null);
        _userManager.CreateAsync(Arg.Any<LocalIdentity>()).Returns(IdentityResult.Success);
        _userManager.SetLockoutEndDateAsync(Arg.Any<LocalIdentity>(), Arg.Any<DateTimeOffset?>())
            .Returns(IdentityResult.Success);

        await _sut.CreateUserAsync(input, TestContext.Current.CancellationToken);

        await _userManager.Received(1).SetLockoutEndDateAsync(
            Arg.Any<LocalIdentity>(), DateTimeOffset.MaxValue);
    }

    [Fact]
    public async Task CreateUserAsync_WhenEnabled_DoesNotSetLockout()
    {
        IdentityUserCreate input = new("eve", "eve@test.com", "Eve", "Ray", true, null);
        _userManager.CreateAsync(Arg.Any<LocalIdentity>()).Returns(IdentityResult.Success);

        await _sut.CreateUserAsync(input, TestContext.Current.CancellationToken);

        await _userManager.DidNotReceive().SetLockoutEndDateAsync(
            Arg.Any<LocalIdentity>(), Arg.Any<DateTimeOffset?>());
    }

    [Fact]
    public async Task CreateUserAsync_WhenFailed_ThrowsInvalidOperationException()
    {
        IdentityUserCreate input = new("fail", "fail@test.com", null, null, true, null);
        var failure = IdentityResult.Failed(
            new IdentityError { Code = "DuplicateUserName", Description = "Username already taken." });
        _userManager.CreateAsync(Arg.Any<LocalIdentity>()).Returns(failure);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.CreateUserAsync(input, TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("User creation failed");
        ex.Message.ShouldContain("Username already taken.");
    }

    // ──── IIdentityUserWriter — SetUserEnabledAsync ────

    [Fact]
    public async Task SetUserEnabledAsync_EnableUser_ClearsLockout()
    {
        LocalIdentity user = new() { UserName = "alice" };
        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.SetLockoutEndDateAsync(user, Arg.Any<DateTimeOffset?>())
            .Returns(IdentityResult.Success);

        await _sut.SetUserEnabledAsync("user-1", true, TestContext.Current.CancellationToken);

        await _userManager.Received(1).SetLockoutEndDateAsync(user, null);
    }

    [Fact]
    public async Task SetUserEnabledAsync_DisableUser_SetsLockoutToMaxValue()
    {
        LocalIdentity user = new() { UserName = "alice" };
        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.SetLockoutEndDateAsync(user, Arg.Any<DateTimeOffset?>())
            .Returns(IdentityResult.Success);

        await _sut.SetUserEnabledAsync("user-1", false, TestContext.Current.CancellationToken);

        await _userManager.Received(1).SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
    }

    [Fact]
    public async Task SetUserEnabledAsync_WhenUserNotFound_ThrowsInvalidOperationException()
    {
        _userManager.FindByIdAsync("missing").ReturnsNull();

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.SetUserEnabledAsync("missing", true, TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("missing");
        ex.Message.ShouldContain("not found");
    }

    // ──── IIdentityUserWriter — UpdateUserAsync ────

    [Fact]
    public async Task UpdateUserAsync_UpdatesFirstNameAndLastName()
    {
        LocalIdentity user = new() { UserName = "alice", FirstName = "Old", LastName = "Name" };
        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        IdentityUserUpdate update = new(null, "New", "Last", null);

        await _sut.UpdateUserAsync("user-1", update, TestContext.Current.CancellationToken);

        user.FirstName.ShouldBe("New");
        user.LastName.ShouldBe("Last");
        await _userManager.Received(1).UpdateAsync(user);
    }

    [Fact]
    public async Task UpdateUserAsync_UpdatesEmail()
    {
        LocalIdentity user = new() { UserName = "alice" };
        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.SetEmailAsync(user, "new@test.com").Returns(IdentityResult.Success);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        IdentityUserUpdate update = new("new@test.com", null, null, null);

        await _sut.UpdateUserAsync("user-1", update, TestContext.Current.CancellationToken);

        await _userManager.Received(1).SetEmailAsync(user, "new@test.com");
    }

    [Fact]
    public async Task UpdateUserAsync_NullFields_DoesNotModify()
    {
        LocalIdentity user = new() { UserName = "alice", FirstName = "Original", LastName = "Name" };
        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        IdentityUserUpdate update = new(null, null, null, null);

        await _sut.UpdateUserAsync("user-1", update, TestContext.Current.CancellationToken);

        user.FirstName.ShouldBe("Original");
        user.LastName.ShouldBe("Name");
        await _userManager.DidNotReceive().SetEmailAsync(Arg.Any<LocalIdentity>(), Arg.Any<string>());
    }

    [Fact]
    public async Task UpdateUserAsync_WhenUserNotFound_ThrowsInvalidOperationException()
    {
        _userManager.FindByIdAsync("missing").ReturnsNull();
        IdentityUserUpdate update = new(null, "First", null, null);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.UpdateUserAsync("missing", update, TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("missing");
        ex.Message.ShouldContain("not found");
    }

    [Fact]
    public async Task UpdateUserAsync_WhenUpdateFails_ThrowsInvalidOperationException()
    {
        LocalIdentity user = new() { UserName = "alice" };
        _userManager.FindByIdAsync("user-1").Returns(user);
        var failure = IdentityResult.Failed(
            new IdentityError { Code = "ConcurrencyFailure", Description = "Concurrency conflict." });
        _userManager.UpdateAsync(user).Returns(failure);
        IdentityUserUpdate update = new(null, "First", null, null);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.UpdateUserAsync("user-1", update, TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("User update failed");
        ex.Message.ShouldContain("Concurrency conflict.");
    }

    // ──── IIdentityRoleManager — GetRoleMembersAsync ────

    [Fact]
    public async Task GetRoleMembersAsync_ReturnsMappedUsers()
    {
        LocalIdentity user1 = new() { UserName = "alice" };
        LocalIdentity user2 = new() { UserName = "bob" };
        _userManager.GetUsersInRoleAsync("admin").Returns([user1, user2]);

        IReadOnlyList<IIdentityUser> result = await _sut.GetRoleMembersAsync(
            "admin", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result[0].ShouldBeSameAs(user1);
        result[1].ShouldBeSameAs(user2);
    }

    [Fact]
    public async Task GetRoleMembersAsync_WhenNoMembers_ReturnsEmptyList()
    {
        _userManager.GetUsersInRoleAsync("empty-role").Returns((IList<LocalIdentity>)[]);

        IReadOnlyList<IIdentityUser> result = await _sut.GetRoleMembersAsync(
            "empty-role", TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    // ──── IIdentityRoleManager — GetUserRolesAsync ────

    [Fact]
    public async Task GetUserRolesAsync_WhenUserExists_ReturnsMappedRoles()
    {
        LocalIdentity user = new() { UserName = "alice" };
        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.GetRolesAsync(user).Returns((IList<string>)["admin", "editor"]);

        IReadOnlyList<GranitIdentityRole> result = await _sut.GetUserRolesAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("admin");
        result[1].Name.ShouldBe("editor");
    }

    [Fact]
    public async Task GetUserRolesAsync_WhenUserNotFound_ReturnsEmptyList()
    {
        _userManager.FindByIdAsync("missing").ReturnsNull();

        IReadOnlyList<GranitIdentityRole> result = await _sut.GetUserRolesAsync(
            "missing", TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    // ──── IIdentityRoleManager — AssignRoleAsync ────

    [Fact]
    public async Task AssignRoleAsync_WhenSuccessful_AddsUserToRole()
    {
        LocalIdentity user = new() { UserName = "alice" };
        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.AddToRoleAsync(user, "admin").Returns(IdentityResult.Success);

        await _sut.AssignRoleAsync("user-1", "admin", TestContext.Current.CancellationToken);

        await _userManager.Received(1).AddToRoleAsync(user, "admin");
    }

    [Fact]
    public async Task AssignRoleAsync_WhenUserNotFound_ThrowsInvalidOperationException()
    {
        _userManager.FindByIdAsync("missing").ReturnsNull();

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.AssignRoleAsync("missing", "admin", TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("missing");
        ex.Message.ShouldContain("not found");
    }

    [Fact]
    public async Task AssignRoleAsync_WhenFailed_ThrowsInvalidOperationException()
    {
        LocalIdentity user = new() { UserName = "alice" };
        _userManager.FindByIdAsync("user-1").Returns(user);
        var failure = IdentityResult.Failed(
            new IdentityError { Code = "InvalidRole", Description = "Role does not exist." });
        _userManager.AddToRoleAsync(user, "nonexistent").Returns(failure);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.AssignRoleAsync("user-1", "nonexistent", TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Role assignment failed");
        ex.Message.ShouldContain("Role does not exist.");
    }

    // ──── IIdentityRoleManager — RemoveRoleAsync ────

    [Fact]
    public async Task RemoveRoleAsync_WhenSuccessful_RemovesUserFromRole()
    {
        LocalIdentity user = new() { UserName = "alice" };
        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.RemoveFromRoleAsync(user, "admin").Returns(IdentityResult.Success);

        await _sut.RemoveRoleAsync("user-1", "admin", TestContext.Current.CancellationToken);

        await _userManager.Received(1).RemoveFromRoleAsync(user, "admin");
    }

    [Fact]
    public async Task RemoveRoleAsync_WhenUserNotFound_ThrowsInvalidOperationException()
    {
        _userManager.FindByIdAsync("missing").ReturnsNull();

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.RemoveRoleAsync("missing", "admin", TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("missing");
        ex.Message.ShouldContain("not found");
    }

    [Fact]
    public async Task RemoveRoleAsync_WhenFailed_ThrowsInvalidOperationException()
    {
        LocalIdentity user = new() { UserName = "alice" };
        _userManager.FindByIdAsync("user-1").Returns(user);
        var failure = IdentityResult.Failed(
            new IdentityError { Code = "UserNotInRole", Description = "User is not in role." });
        _userManager.RemoveFromRoleAsync(user, "admin").Returns(failure);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.RemoveRoleAsync("user-1", "admin", TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Role removal failed");
        ex.Message.ShouldContain("User is not in role.");
    }

    // ──── IIdentityGroupManager ────

    [Fact]
    public async Task GetGroupsAsync_DelegatesToGroupStore()
    {
        List<GranitIdentityGroup> groups =
        [
            new("g1", "Group 1", null, []),
            new("g2", "Group 2", "/path", []),
        ];
        _groupStore.GetGroupsAsync(Arg.Any<CancellationToken>())
            .Returns(groups.AsReadOnly());

        IReadOnlyList<GranitIdentityGroup> result = await _sut.GetGroupsAsync(
            TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
        result[0].Name.ShouldBe("Group 1");
        result[1].Name.ShouldBe("Group 2");
    }

    [Fact]
    public async Task GetUserGroupsAsync_DelegatesToGroupStore()
    {
        List<GranitIdentityGroup> groups = [new("g1", "Admins", null, [])];
        _groupStore.GetUserGroupsAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(groups.AsReadOnly());

        IReadOnlyList<GranitIdentityGroup> result = await _sut.GetUserGroupsAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Admins");
    }

    [Fact]
    public async Task AddUserToGroupAsync_DelegatesToGroupStore()
    {
        await _sut.AddUserToGroupAsync("user-1", "group-1", TestContext.Current.CancellationToken);

        await _groupStore.Received(1).AddUserToGroupAsync(
            "user-1", "group-1", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RemoveUserFromGroupAsync_DelegatesToGroupStore()
    {
        await _sut.RemoveUserFromGroupAsync("user-1", "group-1", TestContext.Current.CancellationToken);

        await _groupStore.Received(1).RemoveUserFromGroupAsync(
            "user-1", "group-1", Arg.Any<CancellationToken>());
    }

    // ──── IIdentitySessionManager ────

    [Fact]
    public async Task GetUserSessionsAsync_ReturnsEmptyList()
    {
        IReadOnlyList<IdentitySession> result = await _sut.GetUserSessionsAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetUserDeviceActivityAsync_ReturnsEmptyList()
    {
        IReadOnlyList<IdentityDeviceActivity> result = await _sut.GetUserDeviceActivityAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task TerminateSessionAsync_CompletesWithoutError()
    {
        await Should.NotThrowAsync(
            () => _sut.TerminateSessionAsync("user-1", "session-1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TerminateAllSessionsAsync_CompletesWithoutError()
    {
        await Should.NotThrowAsync(
            () => _sut.TerminateAllSessionsAsync("user-1", TestContext.Current.CancellationToken));
    }

    // ──── IIdentityPasswordManager ────

    [Fact]
    public async Task GetPasswordChangedAtAsync_ReturnsNull()
    {
        DateTimeOffset? result = await _sut.GetPasswordChangedAtAsync(
            "user-1", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task SendPasswordResetEmailAsync_CompletesWithoutError()
    {
        await Should.NotThrowAsync(
            () => _sut.SendPasswordResetEmailAsync("user-1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetTemporaryPasswordAsync_WhenSuccessful_ResetsPassword()
    {
        LocalIdentity user = new() { UserName = "alice" };
        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.GeneratePasswordResetTokenAsync(user).Returns("reset-token");
        _userManager.ResetPasswordAsync(user, "reset-token", "NewPass123!")
            .Returns(IdentityResult.Success);

        await _sut.SetTemporaryPasswordAsync("user-1", "NewPass123!", TestContext.Current.CancellationToken);

        await _userManager.Received(1).GeneratePasswordResetTokenAsync(user);
        await _userManager.Received(1).ResetPasswordAsync(user, "reset-token", "NewPass123!");
    }

    [Fact]
    public async Task SetTemporaryPasswordAsync_WhenUserNotFound_ThrowsInvalidOperationException()
    {
        _userManager.FindByIdAsync("missing").ReturnsNull();

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.SetTemporaryPasswordAsync("missing", "Pass123!", TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("missing");
        ex.Message.ShouldContain("not found");
    }

    [Fact]
    public async Task SetTemporaryPasswordAsync_WhenResetFails_ThrowsInvalidOperationException()
    {
        LocalIdentity user = new() { UserName = "alice" };
        _userManager.FindByIdAsync("user-1").Returns(user);
        _userManager.GeneratePasswordResetTokenAsync(user).Returns("reset-token");
        var failure = IdentityResult.Failed(
            new IdentityError { Code = "PasswordTooShort", Description = "Password too short." });
        _userManager.ResetPasswordAsync(user, "reset-token", "short")
            .Returns(failure);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.SetTemporaryPasswordAsync("user-1", "short", TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Password reset failed");
        ex.Message.ShouldContain("Password too short.");
    }

    // ──── IIdentityCredentialVerifier ────

    [Fact]
    public async Task VerifyUserCredentialsAsync_WhenValid_ReturnsTrue()
    {
        LocalIdentity user = new() { UserName = "alice" };
        _userManager.FindByNameAsync("alice").Returns(user);
        _userManager.CheckPasswordAsync(user, "correct-password").Returns(true);

        bool result = await _sut.VerifyUserCredentialsAsync(
            "alice", "correct-password", TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyUserCredentialsAsync_WhenInvalidPassword_ReturnsFalse()
    {
        LocalIdentity user = new() { UserName = "alice" };
        _userManager.FindByNameAsync("alice").Returns(user);
        _userManager.CheckPasswordAsync(user, "wrong-password").Returns(false);

        bool result = await _sut.VerifyUserCredentialsAsync(
            "alice", "wrong-password", TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task VerifyUserCredentialsAsync_WhenUserNotFound_ReturnsFalse()
    {
        _userManager.FindByNameAsync("unknown").ReturnsNull();

        bool result = await _sut.VerifyUserCredentialsAsync(
            "unknown", "any-password", TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }
}
