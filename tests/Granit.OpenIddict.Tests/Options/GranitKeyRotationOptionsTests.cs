using Granit.OpenIddict.Options;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests.Options;

public sealed class GranitKeyRotationOptionsTests
{
    [Fact]
    public void SectionName_Is_OpenIddict_KeyRotation() =>
        GranitKeyRotationOptions.SectionName.ShouldBe("OpenIddict:KeyRotation");

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

}
