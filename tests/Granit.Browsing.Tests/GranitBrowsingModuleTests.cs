using System.Linq;
using Granit.Browsing.Sandbox;
using Granit.Http.Security;
using Granit.IO;
using Granit.Modularity;
using Shouldly;
using Xunit;

namespace Granit.Browsing.Tests;

public sealed class GranitBrowsingModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitBrowsingModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsGranitModule() =>
        typeof(GranitBrowsingModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_HttpSecurity()
    {
        DependsOnAttribute attr = typeof(GranitBrowsingModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .Single();

        attr.DependedTypes.ShouldContain(typeof(GranitHttpSecurityModule));
    }

    [Fact]
    public void Module_DependsOn_Io()
    {
        DependsOnAttribute attr = typeof(GranitBrowsingModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), inherit: false)
            .Cast<DependsOnAttribute>()
            .Single();

        attr.DependedTypes.ShouldContain(typeof(GranitIOModule));
    }

    [Fact]
    public void DefaultSandboxProfile_should_use_safe_defaults()
    {
        DefaultSandboxProfile profile = new();

        profile.AllowedSchemes.ShouldBe(["https"]);
        profile.BlockPrivateNetworks.ShouldBeTrue();
        profile.ForceCsp.ShouldBeTrue();
        profile.RedactConsoleMessages.ShouldBeTrue();
        profile.MaxRenderDuration.ShouldBe(System.TimeSpan.FromSeconds(30));
        profile.AllowedHostPatterns.ShouldBeNull();
        profile.DeniedHostPatterns.ShouldBeNull();
        profile.AllowedExecutablePathPrefix.ShouldBeNull();
        profile.DisableJavaScript.ShouldBeFalse();
        profile.BlockNetworkRequests.ShouldBeFalse();
        profile.DisableImages.ShouldBeFalse();
        profile.BlockedUrlPatterns.ShouldBeNull();
    }
}
