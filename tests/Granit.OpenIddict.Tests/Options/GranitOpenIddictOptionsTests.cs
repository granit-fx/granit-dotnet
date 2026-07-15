using Granit.OpenIddict.Options;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Options;

public sealed class GranitOpenIddictOptionsTests
{
    [Fact]
    public void SectionName_IsOpenIddict() =>
        GranitOpenIddictOptions.SectionName.ShouldBe("OpenIddict");

    [Fact]
    public void WithFapi2Profile_RequiresPar_And_DPoP()
    {
        GranitOpenIddictOptions options = new GranitOpenIddictOptions().WithFapi2Profile();

        options.EnableFapi2Profile.ShouldBeTrue();
        options.RequirePar.ShouldBeTrue();
        options.UseReferenceTokens.ShouldBeTrue();
        options.SenderConstraining.ShouldBe(SenderConstrainingMode.DPoP);
    }

    [Fact]
    public void WithFapi2Profile_DoesNotRequireJar()
    {
        // FAPI 2.0 mandates PAR, not JAR (JAR is FAPI 1 Advanced). Forcing RequireJar broke
        // the normal PAR-only flow — clients pushing plain params get no `request` object.
        new GranitOpenIddictOptions().WithFapi2Profile().RequireJar.ShouldBeFalse();
    }
}
