using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Tests;

public sealed class SettingAndFeatureNamesTests
{
    [Theory]
    [InlineData(OpenIddictSettingNames.AccessTokenLifetime, "OpenIddict.AccessTokenLifetime")]
    [InlineData(OpenIddictSettingNames.RefreshTokenLifetime, "OpenIddict.RefreshTokenLifetime")]
    [InlineData(OpenIddictSettingNames.AuthCodeLifetime, "OpenIddict.AuthCodeLifetime")]
    [InlineData(OpenIddictSettingNames.MaxLoginAttempts, "OpenIddict.MaxLoginAttempts")]
    [InlineData(OpenIddictSettingNames.IdleSessionTimeout, "OpenIddict.IdleSessionTimeout")]
    public void SettingNames_AreCorrectlyPrefixed(string actual, string expected) =>
        actual.ShouldBe(expected);

    [Theory]
    [InlineData(OpenIddictFeatureNames.TwoFactor, "OpenIddict.TwoFactor")]
    [InlineData(OpenIddictFeatureNames.DeviceFlow, "OpenIddict.DeviceFlow")]
    [InlineData(OpenIddictFeatureNames.ExternalLogins, "OpenIddict.ExternalLogins")]
    [InlineData(OpenIddictFeatureNames.PkceRequired, "OpenIddict.PkceRequired")]
    [InlineData(OpenIddictFeatureNames.Passkeys, "OpenIddict.Passkeys")]
    public void FeatureNames_AreCorrectlyPrefixed(string actual, string expected) =>
        actual.ShouldBe(expected);

    [Fact]
    public void AllSettingNames_StartWithOpenIddict()
    {
        typeof(OpenIddictSettingNames)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .ShouldAllBe(f => ((string)f.GetValue(null)!).StartsWith("OpenIddict.", StringComparison.Ordinal));
    }

    [Fact]
    public void AllFeatureNames_StartWithOpenIddict()
    {
        typeof(OpenIddictFeatureNames)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .ShouldAllBe(f => ((string)f.GetValue(null)!).StartsWith("OpenIddict.", StringComparison.Ordinal));
    }
}
