using Granit.OpenIddict.Options;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Options;

public sealed class GranitKeyRotationOptionsTests
{
    [Fact]
    public void SectionName_Is_OpenIddict_KeyRotation()
    {
        GranitKeyRotationOptions.SectionName.ShouldBe("OpenIddict:KeyRotation");
    }

    [Fact]
    public void Enabled_Default_Is_False()
    {
        GranitKeyRotationOptions options = new();

        options.Enabled.ShouldBeFalse();
    }

    [Fact]
    public void KeyLifetime_Default_Is_90_Days()
    {
        GranitKeyRotationOptions options = new();

        options.KeyLifetime.ShouldBe(TimeSpan.FromDays(90));
    }

    [Fact]
    public void GracePeriod_Default_Is_14_Days()
    {
        GranitKeyRotationOptions options = new();

        options.GracePeriod.ShouldBe(TimeSpan.FromDays(14));
    }

    [Fact]
    public void RotationLeadTime_Default_Is_7_Days()
    {
        GranitKeyRotationOptions options = new();

        options.RotationLeadTime.ShouldBe(TimeSpan.FromDays(7));
    }

    [Fact]
    public void RsaKeySize_Default_Is_2048()
    {
        GranitKeyRotationOptions options = new();

        options.RsaKeySize.ShouldBe(2048);
    }

    [Fact]
    public void SigningAlgorithm_Default_Is_RS256()
    {
        GranitKeyRotationOptions options = new();

        options.SigningAlgorithm.ShouldBe("RS256");
    }

    [Fact]
    public void Properties_Can_Be_Set()
    {
        GranitKeyRotationOptions options = new()
        {
            Enabled = true,
            KeyLifetime = TimeSpan.FromDays(180),
            GracePeriod = TimeSpan.FromDays(30),
            RotationLeadTime = TimeSpan.FromDays(14),
            RsaKeySize = 4096,
            SigningAlgorithm = "RS384",
        };

        options.Enabled.ShouldBeTrue();
        options.KeyLifetime.ShouldBe(TimeSpan.FromDays(180));
        options.GracePeriod.ShouldBe(TimeSpan.FromDays(30));
        options.RotationLeadTime.ShouldBe(TimeSpan.FromDays(14));
        options.RsaKeySize.ShouldBe(4096);
        options.SigningAlgorithm.ShouldBe("RS384");
    }
}
