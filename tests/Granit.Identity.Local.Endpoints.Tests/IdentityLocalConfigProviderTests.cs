using Granit.Identity.Local.Endpoints.Dtos;
using Granit.Identity.Local.Endpoints.Endpoints;
using Granit.Settings.Services;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Endpoints.Tests;

public sealed class IdentityLocalConfigProviderTests
{
    [Fact]
    public async Task GetConfigAsync_SettingTrue_ReturnsAllowSelfRegistrationTrue()
    {
        ISettingProvider settings = Substitute.For<ISettingProvider>();
        settings.GetOrNullAsync(IdentityLocalSettingNames.AllowSelfRegistration, Arg.Any<CancellationToken>())
            .Returns("true");

        IdentityLocalConfigProvider provider = new(settings);

        IdentityLocalConfigResponse result = await provider.GetConfigAsync(TestContext.Current.CancellationToken);

        result.AllowSelfRegistration.ShouldBeTrue();
    }

    [Fact]
    public async Task GetConfigAsync_SettingFalse_ReturnsAllowSelfRegistrationFalse()
    {
        ISettingProvider settings = Substitute.For<ISettingProvider>();
        settings.GetOrNullAsync(IdentityLocalSettingNames.AllowSelfRegistration, Arg.Any<CancellationToken>())
            .Returns("false");

        IdentityLocalConfigProvider provider = new(settings);

        IdentityLocalConfigResponse result = await provider.GetConfigAsync(TestContext.Current.CancellationToken);

        result.AllowSelfRegistration.ShouldBeFalse();
    }

    [Fact]
    public async Task GetConfigAsync_SettingNull_ReturnsAllowSelfRegistrationFalse()
    {
        ISettingProvider settings = Substitute.For<ISettingProvider>();
        settings.GetOrNullAsync(IdentityLocalSettingNames.AllowSelfRegistration, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        IdentityLocalConfigProvider provider = new(settings);

        IdentityLocalConfigResponse result = await provider.GetConfigAsync(TestContext.Current.CancellationToken);

        result.AllowSelfRegistration.ShouldBeFalse();
    }

    [Fact]
    public async Task GetConfigAsync_SettingTrueUpperCase_ReturnsAllowSelfRegistrationTrue()
    {
        ISettingProvider settings = Substitute.For<ISettingProvider>();
        settings.GetOrNullAsync(IdentityLocalSettingNames.AllowSelfRegistration, Arg.Any<CancellationToken>())
            .Returns("TRUE");

        IdentityLocalConfigProvider provider = new(settings);

        IdentityLocalConfigResponse result = await provider.GetConfigAsync(TestContext.Current.CancellationToken);

        result.AllowSelfRegistration.ShouldBeTrue();
    }
}
