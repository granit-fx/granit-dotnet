using Granit.OpenIddict.Internal;
using Granit.OpenIddict.Options;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Internal;

/// <summary>
/// The FAPI 2.0 profile upgrades the key-rotation signing algorithm to PS256 when it is still the
/// RS256 default, and leaves an explicit non-default choice untouched.
/// </summary>
public sealed class Fapi2SigningAlgorithmPostConfigureTests
{
    private static Fapi2SigningAlgorithmPostConfigure Create(bool fapi2) =>
        new(Microsoft.Extensions.Options.Options.Create(
            new GranitOpenIddictOptions { EnableFapi2Profile = fapi2 }));

    [Fact]
    public void Fapi2Enabled_DefaultAlgorithm_UpgradesToPs256()
    {
        GranitKeyRotationOptions options = new(); // SigningAlgorithm defaults to RS256

        Create(fapi2: true).PostConfigure(name: null, options);

        options.SigningAlgorithm.ShouldBe("PS256");
    }

    [Fact]
    public void Fapi2Enabled_ExplicitEs256_IsRespected()
    {
        GranitKeyRotationOptions options = new() { SigningAlgorithm = "ES256" };

        Create(fapi2: true).PostConfigure(name: null, options);

        options.SigningAlgorithm.ShouldBe("ES256", "an explicit non-default algorithm must not be overridden");
    }

    [Fact]
    public void Fapi2Disabled_DefaultAlgorithm_StaysRs256()
    {
        GranitKeyRotationOptions options = new();

        Create(fapi2: false).PostConfigure(name: null, options);

        options.SigningAlgorithm.ShouldBe("RS256");
    }
}
