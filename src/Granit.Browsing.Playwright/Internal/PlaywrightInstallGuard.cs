using Granit.Browsing.Playwright.Options;
using Microsoft.Extensions.Hosting;

namespace Granit.Browsing.Playwright.Internal;

/// <summary>
/// Decides whether the bundled <c>Microsoft.Playwright.Program.Main(["install"])</c>
/// auto-install may run, and refuses to start the browser when a production host has
/// not pre-provisioned the binaries. Surfaces an actionable error rather than silently
/// downloading binaries on the first request in a production host.
/// </summary>
internal static class PlaywrightInstallGuard
{
    /// <summary>
    /// Returns <c>true</c> when <c>playwright install</c> should run at start-up.
    /// </summary>
    /// <remarks>
    /// Resolution order: explicit <see cref="PlaywrightOptions.AutoInstallBrowsers"/> wins;
    /// otherwise the default is <c>!env.IsProduction()</c>.
    /// </remarks>
    public static bool ShouldAutoInstall(PlaywrightOptions options, IHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(env);

        return options.AutoInstallBrowsers ?? !env.IsProduction();
    }

    /// <summary>
    /// Throws when the host has not provisioned browsers up-front and auto-install is
    /// disabled — surfaces an actionable error instead of silently downloading binaries
    /// on the first request.
    /// </summary>
    public static void EnsureBrowsersProvisioned(PlaywrightOptions options, IHostEnvironment env)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(env);

        if (!string.IsNullOrEmpty(options.ExecutablePath))
        {
            return;
        }
        if (options.SkipBrowserInstall)
        {
            return;
        }
        if (ShouldAutoInstall(options, env))
        {
            return;
        }

        throw new InvalidOperationException(
            "Playwright browsers not pre-provisioned. " +
            "Either set PlaywrightOptions.ExecutablePath to a sandbox-allowed binary, " +
            "set SkipBrowserInstall=true and bundle browsers in your container image, " +
            "or explicitly enable AutoInstallBrowsers=true (not recommended in production).");
    }
}
