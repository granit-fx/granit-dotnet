using Granit.Identity;
using Granit.Identity.Local.AspNetIdentity.Internal;
using Granit.Identity.Local.Domain;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.AspNetIdentity.Tests.Internal;

public sealed class AspNetIdentityUserLookupServiceTests
{
    private static UserManager<GranitUser> CreateUserManager()
    {
        IUserStore<GranitUser> store = Substitute.For<IUserStore<GranitUser>>();
        return Substitute.For<UserManager<GranitUser>>(
            store, null, null, null, null, null, null, null, null);
    }

    [Fact]
    public async Task FindByIdAsync_WhenUserNotFound_ReturnsNull()
    {
        UserManager<GranitUser> userManager = CreateUserManager();
        userManager.FindByIdAsync(Arg.Any<string>()).Returns((GranitUser?)null);
        AspNetIdentityUserLookupService sut = new(userManager);

        IIdentityUser? result = await sut.FindByIdAsync("nonexistent", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindByIdAsync_WhenUserFound_ReturnsUser()
    {
        UserManager<GranitUser> userManager = CreateUserManager();
        var user = new GranitUser { UserName = "alice" };
        userManager.FindByIdAsync("user-id").Returns(user);
        AspNetIdentityUserLookupService sut = new(userManager);

        IIdentityUser? result = await sut.FindByIdAsync("user-id", TestContext.Current.CancellationToken);

        result.ShouldBeSameAs(user);
    }

    [Fact]
    public async Task RefreshAllAsync_ReturnsZero()
    {
        AspNetIdentityUserLookupService sut = new(CreateUserManager());

        int result = await sut.RefreshAllAsync(TestContext.Current.CancellationToken);

        result.ShouldBe(0);
    }

    [Fact]
    public async Task RefreshStaleAsync_ReturnsZero()
    {
        AspNetIdentityUserLookupService sut = new(CreateUserManager());

        int result = await sut.RefreshStaleAsync(TestContext.Current.CancellationToken);

        result.ShouldBe(0);
    }

    [Fact]
    public async Task DeleteByIdAsync_DoesNotThrow()
    {
        AspNetIdentityUserLookupService sut = new(CreateUserManager());

        await Should.NotThrowAsync(() =>
            sut.DeleteByIdAsync("user-id", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PseudonymizeByIdAsync_DoesNotThrow()
    {
        AspNetIdentityUserLookupService sut = new(CreateUserManager());

        await Should.NotThrowAsync(() =>
            sut.PseudonymizeByIdAsync("user-id", TestContext.Current.CancellationToken));
    }
}
