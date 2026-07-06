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
}
