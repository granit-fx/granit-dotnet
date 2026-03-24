using Shouldly;
using Xunit;

namespace Granit.Users.Tests;

public sealed class SystemCurrentUserServiceTests
{
    private readonly SystemCurrentUserService _sut = new();

    [Fact]
    public void ActorKind_ReturnsSystem() => _sut.ActorKind.ShouldBe(ActorKind.System);

    [Fact]
    public void IsMachine_ReturnsTrue() => _sut.IsMachine.ShouldBeTrue();

    [Fact]
    public void SystemUserId_Constant_IsSystem() => SystemCurrentUserService.SystemUserId.ShouldBe("system");

    [Fact]
    public void UserId_ReturnsSystemUserId() => _sut.UserId.ShouldBe(SystemCurrentUserService.SystemUserId);

    [Fact]
    public void UserName_ReturnsSystemUserId() => _sut.UserName.ShouldBe(SystemCurrentUserService.SystemUserId);

    [Fact]
    public void IsAuthenticated_ReturnsFalse() => _sut.IsAuthenticated.ShouldBeFalse();

    [Fact]
    public void Email_ReturnsNull() => _sut.Email.ShouldBeNull();

    [Fact]
    public void FirstName_ReturnsNull() => _sut.FirstName.ShouldBeNull();

    [Fact]
    public void LastName_ReturnsNull() => _sut.LastName.ShouldBeNull();

    [Fact]
    public void ApiKeyId_ReturnsNull() => _sut.ApiKeyId.ShouldBeNull();

    [Fact]
    public void GetRoles_ReturnsEmpty() => _sut.GetRoles().ShouldBeEmpty();

    [Fact]
    public void IsInRole_ReturnsFalse() => _sut.IsInRole("Admin").ShouldBeFalse();
}
