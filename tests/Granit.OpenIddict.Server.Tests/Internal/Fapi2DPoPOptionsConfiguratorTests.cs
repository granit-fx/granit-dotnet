using Granit.Authentication.DPoP.Options;
using Granit.OpenIddict.Options;
using Granit.OpenIddict.Server.Internal;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.Server.Tests.Internal;

public sealed class Fapi2DPoPOptionsConfiguratorTests
{
    [Fact]
    public void PostConfigure_WhenFapi2Enabled_ForcesAllDPoPFlags()
    {
        Fapi2DPoPOptionsConfigurator configurator = new(
            Microsoft.Extensions.Options.Options.Create(new GranitOpenIddictOptions { EnableFapi2Profile = true }));
        DPoPValidationOptions options = new();

        configurator.PostConfigure(name: null, options);

        options.RequireDPoP.ShouldBeTrue();
        options.RequireTokenBinding.ShouldBeTrue();
        options.RequireNonce.ShouldBeTrue();
    }

    [Fact]
    public void PostConfigure_WhenFapi2Disabled_LeavesFlagsUntouched()
    {
        Fapi2DPoPOptionsConfigurator configurator = new(
            Microsoft.Extensions.Options.Options.Create(new GranitOpenIddictOptions { EnableFapi2Profile = false }));
        DPoPValidationOptions options = new()
        {
            RequireDPoP = false,
            RequireTokenBinding = false,
            RequireNonce = false,
        };

        configurator.PostConfigure(name: null, options);

        options.RequireDPoP.ShouldBeFalse();
        options.RequireTokenBinding.ShouldBeFalse();
        options.RequireNonce.ShouldBeFalse();
    }

    [Fact]
    public void PostConfigure_WhenFapi2Disabled_DoesNotOverrideExplicitlyEnabledFlags()
    {
        Fapi2DPoPOptionsConfigurator configurator = new(
            Microsoft.Extensions.Options.Options.Create(new GranitOpenIddictOptions { EnableFapi2Profile = false }));
        DPoPValidationOptions options = new()
        {
            RequireDPoP = true,
            RequireTokenBinding = true,
            RequireNonce = true,
        };

        configurator.PostConfigure(name: null, options);

        options.RequireDPoP.ShouldBeTrue();
        options.RequireTokenBinding.ShouldBeTrue();
        options.RequireNonce.ShouldBeTrue();
    }
}
