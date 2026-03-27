// =============================================================================
// Tests — SystemCurrentUserService: system-process identity defaults
// =============================================================================

using Granit.Users;
using Shouldly;
using Xunit;

namespace Granit.Tests.Users;

public sealed class SystemCurrentUserServiceTests
{
    private readonly SystemCurrentUserService _sut = new();

    // -------------------------------------------------------------------------
    // Identity properties
    // -------------------------------------------------------------------------

    [Fact]
    public void UserId_ReturnsSystemConstant()
    {
        _sut.UserId.ShouldBe(SystemCurrentUserService.SystemUserId);
        _sut.UserId.ShouldBe("system");
    }

    [Fact]
    public void UserName_ReturnsSystemConstant()
    {
        _sut.UserName.ShouldBe(SystemCurrentUserService.SystemUserId);
        _sut.UserName.ShouldBe("system");
    }

    [Fact]
    public void Email_ReturnsNull() =>
        _sut.Email.ShouldBeNull();

    [Fact]
    public void FirstName_ReturnsNull() =>
        _sut.FirstName.ShouldBeNull();

    [Fact]
    public void LastName_ReturnsNull() =>
        _sut.LastName.ShouldBeNull();

    // -------------------------------------------------------------------------
    // Authentication state
    // -------------------------------------------------------------------------

    [Fact]
    public void IsAuthenticated_ReturnsFalse() =>
        _sut.IsAuthenticated.ShouldBeFalse();

    // -------------------------------------------------------------------------
    // Roles
    // -------------------------------------------------------------------------

    [Fact]
    public void GetRoles_ReturnsEmptyCollection()
    {
        IReadOnlyList<string> roles = _sut.GetRoles();

        roles.ShouldBeEmpty();
    }

    [Fact]
    public void IsInRole_AnyRole_ReturnsFalse()
    {
        _sut.IsInRole("Admin").ShouldBeFalse();
        _sut.IsInRole("User").ShouldBeFalse();
        _sut.IsInRole("system").ShouldBeFalse();
        _sut.IsInRole(string.Empty).ShouldBeFalse();
    }

    // -------------------------------------------------------------------------
    // Actor kind
    // -------------------------------------------------------------------------

    [Fact]
    public void ActorKind_ReturnsSystem() =>
        _sut.ActorKind.ShouldBe(ActorKind.System);

    [Fact]
    public void IsMachine_ReturnsTrue() =>
        _sut.IsMachine.ShouldBeTrue();

    // -------------------------------------------------------------------------
    // API key
    // -------------------------------------------------------------------------

    [Fact]
    public void ApiKeyId_ReturnsNull() =>
        _sut.ApiKeyId.ShouldBeNull();

    // -------------------------------------------------------------------------
    // ICurrentUserService contract
    // -------------------------------------------------------------------------

    [Fact]
    public void ImplementsICurrentUserService() =>
        _sut.ShouldBeAssignableTo<ICurrentUserService>();

    [Fact]
    public void SystemUserId_ConstantIsSystem() =>
        SystemCurrentUserService.SystemUserId.ShouldBe("system");
}
