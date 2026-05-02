using Granit.Events;
using Granit.Identity.Local.AspNetIdentity.Internal;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Events;
using Granit.Identity.Local.Services;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.AspNetIdentity.Tests;

public sealed class AspNetPasswordResetServiceTests
{
    private readonly UserManager<LocalIdentity> _userManager;
    private readonly IDistributedEventBus _eventBus = Substitute.For<IDistributedEventBus>();
    private readonly AspNetPasswordResetService _sut;

    public AspNetPasswordResetServiceTests()
    {
        IUserStore<LocalIdentity> store = Substitute.For<IUserStore<LocalIdentity>>();
        _userManager = Substitute.For<UserManager<LocalIdentity>>(
            store, null, null, null, null, null, null, null, null);

        _sut = new AspNetPasswordResetService(_userManager, _eventBus);
    }

    [Fact]
    public async Task RequestResetAsync_UserExists_ReturnsTrue()
    {
        var user = new LocalIdentity { Id = Guid.NewGuid(), Email = "user@example.com" };
        _userManager.FindByEmailAsync("user@example.com").Returns(user);
        _userManager.GeneratePasswordResetTokenAsync(user).Returns("reset-token-123");

        bool result = await _sut.RequestResetAsync("user@example.com", TestContext.Current.CancellationToken);

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task RequestResetAsync_UserExists_PublishesPasswordResetRequestedEto()
    {
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var user = new LocalIdentity { Id = userId, Email = "user@example.com", TenantId = tenantId };
        _userManager.FindByEmailAsync("user@example.com").Returns(user);
        _userManager.GeneratePasswordResetTokenAsync(user).Returns("reset-token-123");

        await _sut.RequestResetAsync("user@example.com", TestContext.Current.CancellationToken);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<PasswordResetRequestedEto>(e =>
                e.UserId == userId &&
                e.Email == "user@example.com" &&
                e.ResetToken == "reset-token-123" &&
                e.TenantId == tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestResetAsync_UserNotFound_ReturnsFalse()
    {
        _userManager.FindByEmailAsync("unknown@example.com").Returns((LocalIdentity?)null);

        bool result = await _sut.RequestResetAsync("unknown@example.com", TestContext.Current.CancellationToken);

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task RequestResetAsync_UserNotFound_DoesNotPublishEvent()
    {
        _userManager.FindByEmailAsync("unknown@example.com").Returns((LocalIdentity?)null);

        await _sut.RequestResetAsync("unknown@example.com", TestContext.Current.CancellationToken);

        await _eventBus.DidNotReceive().PublishAsync(
            Arg.Any<PasswordResetRequestedEto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetPasswordAsync_ValidUserAndToken_Succeeds()
    {
        var user = new LocalIdentity { Id = Guid.NewGuid() };
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.ResetPasswordAsync(user, "token", "NewP@ss1").Returns(IdentityResult.Success);
        _userManager.SetLockoutEndDateAsync(user, Arg.Any<DateTimeOffset?>()).Returns(IdentityResult.Success);

        await Should.NotThrowAsync(
            () => _sut.ResetPasswordAsync(user.Id.ToString(), "token", "NewP@ss1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ResetPasswordAsync_LockedOutUser_ClearsLockout()
    {
        var user = new LocalIdentity { Id = Guid.NewGuid(), LockoutEnd = DateTimeOffset.UtcNow.AddHours(1) };
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.ResetPasswordAsync(user, "token", "NewP@ss1").Returns(IdentityResult.Success);
        _userManager.SetLockoutEndDateAsync(user, Arg.Any<DateTimeOffset?>()).Returns(IdentityResult.Success);

        await _sut.ResetPasswordAsync(user.Id.ToString(), "token", "NewP@ss1", TestContext.Current.CancellationToken);

        await _userManager.Received(1).SetLockoutEndDateAsync(user, null);
    }

    [Fact]
    public async Task ResetPasswordAsync_NotLockedOutUser_DoesNotCallSetLockout()
    {
        var user = new LocalIdentity { Id = Guid.NewGuid() };
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.ResetPasswordAsync(user, "token", "NewP@ss1").Returns(IdentityResult.Success);

        await _sut.ResetPasswordAsync(user.Id.ToString(), "token", "NewP@ss1", TestContext.Current.CancellationToken);

        await _userManager.DidNotReceive().SetLockoutEndDateAsync(Arg.Any<LocalIdentity>(), Arg.Any<DateTimeOffset?>());
    }

    [Fact]
    public async Task ResetPasswordAsync_UserNotFound_ThrowsInvalidOperation()
    {
        string unknownId = Guid.NewGuid().ToString();
        _userManager.FindByIdAsync(unknownId).Returns((LocalIdentity?)null);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.ResetPasswordAsync(unknownId, "token", "NewP@ss1", TestContext.Current.CancellationToken));

        ex.Message.ShouldContain(unknownId);
    }

    [Fact]
    public async Task ResetPasswordAsync_IdentityResultFailed_ThrowsWithErrors()
    {
        var user = new LocalIdentity { Id = Guid.NewGuid() };
        _userManager.FindByIdAsync(user.Id.ToString()).Returns(user);
        _userManager.ResetPasswordAsync(user, "bad-token", "NewP@ss1")
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Invalid token" }));

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.ResetPasswordAsync(user.Id.ToString(), "bad-token", "NewP@ss1", TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Invalid token");
    }
}
