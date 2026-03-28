using Granit.Authentication.OpenIddict.Options;
using Shouldly;
using Xunit;

namespace Granit.Authentication.OpenIddict.Tests.Options;

public sealed class GranitOpenIddictValidationOptionsTests
{
    [Fact]
    public void SectionName_IsCorrect() =>
        GranitOpenIddictValidationOptions.SectionName.ShouldBe("Authentication:OpenIddict");

    [Fact]
    public void RequireDPoP_DefaultsToFalse() =>
        new GranitOpenIddictValidationOptions().RequireDPoP.ShouldBeFalse();

    [Fact]
    public void Issuer_DefaultsToNull() =>
        new GranitOpenIddictValidationOptions().Issuer.ShouldBeNull();

    [Fact]
    public void Audience_DefaultsToNull() =>
        new GranitOpenIddictValidationOptions().Audience.ShouldBeNull();

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var issuer = new Uri("https://auth.example.com");

        GranitOpenIddictValidationOptions options = new()
        {
            Issuer = issuer,
            Audience = "api.example.com",
            RequireDPoP = true,
        };

        options.Issuer.ShouldBe(issuer);
        options.Audience.ShouldBe("api.example.com");
        options.RequireDPoP.ShouldBeTrue();
    }
}
