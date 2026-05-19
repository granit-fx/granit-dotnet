using Granit.Browsing.Playwright.Internal;
using Granit.Browsing.Playwright.Options;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Browsing.Playwright.Tests;

public sealed class PlaywrightInstallGuardTests
{
    [Theory]
    [InlineData("Development", null, true)]
    [InlineData("Staging", null, true)]
    [InlineData("Production", null, false)]
    [InlineData("Production", true, true)]
    [InlineData("Development", false, false)]
    public void ShouldAutoInstall_resolves_from_env_and_options(string env, bool? optsValue, bool expected)
    {
        var options = new PlaywrightOptions { AutoInstallBrowsers = optsValue };
        var hostEnv = new TestHostEnvironment { EnvironmentName = env };

        PlaywrightInstallGuard.ShouldAutoInstall(options, hostEnv).ShouldBe(expected);
    }

    [Fact]
    public void EnsureBrowsersProvisioned_succeeds_when_ExecutablePath_set()
    {
        var options = new PlaywrightOptions
        {
            ExecutablePath = "/usr/bin/chromium",
            AutoInstallBrowsers = false,
        };
        var hostEnv = new TestHostEnvironment { EnvironmentName = "Production" };

        Should.NotThrow(() => PlaywrightInstallGuard.EnsureBrowsersProvisioned(options, hostEnv));
    }

    [Fact]
    public void EnsureBrowsersProvisioned_succeeds_when_SkipBrowserInstall_set()
    {
        var options = new PlaywrightOptions
        {
            SkipBrowserInstall = true,
            AutoInstallBrowsers = false,
        };
        var hostEnv = new TestHostEnvironment { EnvironmentName = "Production" };

        Should.NotThrow(() => PlaywrightInstallGuard.EnsureBrowsersProvisioned(options, hostEnv));
    }

    [Fact]
    public void EnsureBrowsersProvisioned_succeeds_in_development_default()
    {
        var options = new PlaywrightOptions();
        var hostEnv = new TestHostEnvironment { EnvironmentName = "Development" };

        Should.NotThrow(() => PlaywrightInstallGuard.EnsureBrowsersProvisioned(options, hostEnv));
    }

    [Fact]
    public void EnsureBrowsersProvisioned_throws_when_production_without_provisioning()
    {
        var options = new PlaywrightOptions();
        var hostEnv = new TestHostEnvironment { EnvironmentName = "Production" };

        Should.Throw<InvalidOperationException>(
            () => PlaywrightInstallGuard.EnsureBrowsersProvisioned(options, hostEnv));
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = System.IO.Path.GetTempPath();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
