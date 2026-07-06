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

    // =========================================================================
    // Data annotations
    // =========================================================================

    [Fact]
    public void AdminRoles_HasMinLengthAttribute()
    {
        System.ComponentModel.DataAnnotations.MinLengthAttribute? attr = typeof(GranitAuthorizationOptions)
            .GetProperty(nameof(GranitAuthorizationOptions.AdminRoles))!
            .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.MinLengthAttribute), false)
            .Cast<System.ComponentModel.DataAnnotations.MinLengthAttribute>()
            .FirstOrDefault();

        attr.ShouldNotBeNull();
        attr!.Length.ShouldBe(1);
    }

    [Fact]
    public void CacheDuration_HasRangeAttribute()
    {
        System.ComponentModel.DataAnnotations.RangeAttribute? attr = typeof(GranitAuthorizationOptions)
            .GetProperty(nameof(GranitAuthorizationOptions.CacheDuration))!
            .GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RangeAttribute), false)
            .Cast<System.ComponentModel.DataAnnotations.RangeAttribute>()
            .FirstOrDefault();

        attr.ShouldNotBeNull();
    }

    // =========================================================================
    // AdminRoles — list behavior
    // =========================================================================

    [Fact]
    public void AdminRoles_CanAddRoles()
    {
        GranitAuthorizationOptions options = new();

        options.AdminRoles.Add("superadmin");

        options.AdminRoles.Count.ShouldBe(2);
        options.AdminRoles.ShouldContain("admin");
        options.AdminRoles.ShouldContain("superadmin");
    }

    [Fact]
    public void AdminRoles_CanClearAndReplace()
    {
        GranitAuthorizationOptions options = new();

        options.AdminRoles.Clear();
        options.AdminRoles.Add("root");

        options.AdminRoles.ShouldHaveSingleItem().ShouldBe("root");
    }
}
