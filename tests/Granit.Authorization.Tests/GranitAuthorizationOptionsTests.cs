using Granit.Authorization.Options;
using Shouldly;
using Xunit;

namespace Granit.Authorization.Tests;

public sealed class GranitAuthorizationOptionsTests
{
    [Fact]
    public void SectionName_IsAuthorization() => GranitAuthorizationOptions.SectionName.ShouldBe("Authorization");

    [Fact]
    public void AdminRoles_Default_ContainsAdmin()
    {
        GranitAuthorizationOptions options = new();

        options.AdminRoles.ShouldHaveSingleItem().ShouldBe("admin");
    }

    [Fact]
    public void CacheDuration_Default_IsFiveMinutes()
    {
        GranitAuthorizationOptions options = new();

        options.CacheDuration.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void AlwaysAllow_Default_IsFalse()
    {
        GranitAuthorizationOptions options = new();

        options.AlwaysAllow.ShouldBeFalse();
    }

    [Fact]
    public void AdminRoles_CanBeModified()
    {
        GranitAuthorizationOptions options = new();

        options.AdminRoles = ["superadmin", "root"];

        options.AdminRoles.Count.ShouldBe(2);
        options.AdminRoles.ShouldContain("superadmin");
        options.AdminRoles.ShouldContain("root");
    }

    [Fact]
    public void CacheDuration_CanBeModified()
    {
        GranitAuthorizationOptions options = new();

        options.CacheDuration = TimeSpan.FromMinutes(10);

        options.CacheDuration.ShouldBe(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public void AlwaysAllow_CanBeSetToTrue()
    {
        GranitAuthorizationOptions options = new();

        options.AlwaysAllow = true;

        options.AlwaysAllow.ShouldBeTrue();
    }
}
