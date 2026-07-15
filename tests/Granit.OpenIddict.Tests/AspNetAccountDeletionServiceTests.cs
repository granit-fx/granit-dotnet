using Granit.Identity.Local.Domain;
using Granit.OpenIddict.Internal;
using Granit.Timing;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using OpenIddict.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests;

public sealed class AspNetAccountDeletionServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2025, 6, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly UserManager<LocalIdentity> _userManager;
    private readonly IOpenIddictTokenManager _tokenManager = Substitute.For<IOpenIddictTokenManager>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly AspNetAccountDeletionService _sut;

    public AspNetAccountDeletionServiceTests()
    {
        IUserStore<LocalIdentity> store = Substitute.For<IUserStore<LocalIdentity>>();
        _userManager = Substitute.For<UserManager<LocalIdentity>>(
            store, null, null, null, null, null, null, null, null);

        _clock.Now.Returns(FixedNow);

        _sut = new AspNetAccountDeletionService(
            _userManager,
            _tokenManager,
            _clock);
    }

    // ────────────────────── InitiateAsync — happy path ──────────────────────

    [Fact]
    public async Task InitiateAsync_UserExists_SoftDeletesUser()
    {
        LocalIdentity user = CreateUser();
        string userId = user.Id.ToString();

        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue).Returns(IdentityResult.Success);
        _userManager.UpdateSecurityStampAsync(user).Returns(IdentityResult.Success);
        SetupEmptyTokenStream(userId);

        await _sut.InitiateAsync(userId, TestContext.Current.CancellationToken);

        user.IsDeleted.ShouldBeTrue();
        user.DeletedAt.ShouldBe(FixedNow);
        user.DeletedBy.ShouldBe(userId);
    }

    [Fact]
    public async Task InitiateAsync_UserExists_LocksAccount()
    {
        LocalIdentity user = CreateUser();
        string userId = user.Id.ToString();

        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _userManager.SetLockoutEndDateAsync(user, Arg.Any<DateTimeOffset>()).Returns(IdentityResult.Success);
        _userManager.UpdateSecurityStampAsync(user).Returns(IdentityResult.Success);
        SetupEmptyTokenStream(userId);

        await _sut.InitiateAsync(userId, TestContext.Current.CancellationToken);

        await _userManager.Received(1).SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
    }

    [Fact]
    public async Task InitiateAsync_UserExists_UpdatesSecurityStamp()
    {
        LocalIdentity user = CreateUser();
        string userId = user.Id.ToString();

        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _userManager.SetLockoutEndDateAsync(user, Arg.Any<DateTimeOffset>()).Returns(IdentityResult.Success);
        _userManager.UpdateSecurityStampAsync(user).Returns(IdentityResult.Success);
        SetupEmptyTokenStream(userId);

        await _sut.InitiateAsync(userId, TestContext.Current.CancellationToken);

        await _userManager.Received(1).UpdateSecurityStampAsync(user);
    }

    [Fact]
    public async Task InitiateAsync_LeavesErasureEventPending()
    {
        // The service records the soft-delete durably but does NOT publish inline — the erasure
        // event stays pending (DeletionEventDispatchedAt null) for the reconciler to publish.
        LocalIdentity user = CreateUser();
        string userId = user.Id.ToString();

        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _userManager.SetLockoutEndDateAsync(user, Arg.Any<DateTimeOffset>()).Returns(IdentityResult.Success);
        _userManager.UpdateSecurityStampAsync(user).Returns(IdentityResult.Success);
        SetupEmptyTokenStream(userId);

        await _sut.InitiateAsync(userId, TestContext.Current.CancellationToken);

        user.IsDeleted.ShouldBeTrue();
        user.DeletionEventDispatchedAt.ShouldBeNull("the erasure event is dispatched by the reconciler, not inline");
    }

    // ────────────────────── InitiateAsync — token revocation ──────────────────────

    [Fact]
    public async Task InitiateAsync_UserHasTokens_RevokesAllTokens()
    {
        LocalIdentity user = CreateUser();
        string userId = user.Id.ToString();

        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _userManager.SetLockoutEndDateAsync(user, Arg.Any<DateTimeOffset>()).Returns(IdentityResult.Success);
        _userManager.UpdateSecurityStampAsync(user).Returns(IdentityResult.Success);

        object token1 = new object();
        object token2 = new object();
        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(token1, token2));
        _tokenManager.TryRevokeAsync(Arg.Any<object>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await _sut.InitiateAsync(userId, TestContext.Current.CancellationToken);

        await _tokenManager.Received(1).TryRevokeAsync(token1, Arg.Any<CancellationToken>());
        await _tokenManager.Received(1).TryRevokeAsync(token2, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitiateAsync_NoTokens_StillSucceeds()
    {
        LocalIdentity user = CreateUser();
        string userId = user.Id.ToString();

        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.UpdateAsync(user).Returns(IdentityResult.Success);
        _userManager.SetLockoutEndDateAsync(user, Arg.Any<DateTimeOffset>()).Returns(IdentityResult.Success);
        _userManager.UpdateSecurityStampAsync(user).Returns(IdentityResult.Success);
        SetupEmptyTokenStream(userId);

        await Should.NotThrowAsync(
            () => _sut.InitiateAsync(userId, TestContext.Current.CancellationToken));
    }

    // ────────────────────── InitiateAsync — error paths ──────────────────────

    [Fact]
    public async Task InitiateAsync_UserNotFound_ThrowsInvalidOperation()
    {
        string unknownId = Guid.NewGuid().ToString();
        _userManager.FindByIdAsync(unknownId).Returns((LocalIdentity?)null);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.InitiateAsync(unknownId, TestContext.Current.CancellationToken));

        ex.Message.ShouldContain(unknownId);
    }

    [Fact]
    public async Task InitiateAsync_UpdateFails_ThrowsInvalidOperation()
    {
        LocalIdentity user = CreateUser();
        string userId = user.Id.ToString();

        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.UpdateAsync(user)
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Update failed" }));

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.InitiateAsync(userId, TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Update failed");
    }

    [Fact]
    public async Task InitiateAsync_UpdateFails_DoesNotLockAccountOrRevokeTokens()
    {
        LocalIdentity user = CreateUser();
        string userId = user.Id.ToString();

        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.UpdateAsync(user)
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Conflict" }));

        await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.InitiateAsync(userId, TestContext.Current.CancellationToken));

        await _userManager.DidNotReceive().SetLockoutEndDateAsync(Arg.Any<LocalIdentity>(), Arg.Any<DateTimeOffset>());
        await _userManager.DidNotReceive().UpdateSecurityStampAsync(Arg.Any<LocalIdentity>());
    }

    // ────────────────────── Helpers ──────────────────────

    private static LocalIdentity CreateUser() => new()
    {
        Id = Guid.NewGuid(),
        UserName = "testuser",
        Email = "test@example.com",
    };

    private void SetupEmptyTokenStream(string userId)
    {
        _tokenManager.FindBySubjectAsync(userId, Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable<object>());
    }

    private static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(params T[] items)
    {
        foreach (T item in items)
        {
            yield return item;
        }

        await Task.CompletedTask;
    }
}
