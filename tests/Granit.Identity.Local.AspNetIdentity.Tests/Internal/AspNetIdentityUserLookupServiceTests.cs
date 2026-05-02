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
    private static UserManager<LocalIdentity> CreateUserManager()
    {
        IUserStore<LocalIdentity> store = Substitute.For<IUserStore<LocalIdentity>>();
        return Substitute.For<UserManager<LocalIdentity>>(
            store, null, null, null, null, null, null, null, null);
    }

    [Fact]
    public async Task FindByIdAsync_WhenUserNotFound_ReturnsNull()
    {
        UserManager<LocalIdentity> userManager = CreateUserManager();
        userManager.FindByIdAsync(Arg.Any<string>()).Returns((LocalIdentity?)null);
        AspNetIdentityUserLookupService sut = new(userManager);

        IIdentityUser? result = await sut.FindByIdAsync("nonexistent", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task FindByIdAsync_WhenUserFound_ReturnsUser()
    {
        UserManager<LocalIdentity> userManager = CreateUserManager();
        var user = new LocalIdentity { UserName = "alice" };
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
