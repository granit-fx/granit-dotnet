using System;
using Granit.Browsing.Capabilities;
using Granit.Browsing.Options;
using Granit.Browsing.Playwright.Internal;
using Granit.Browsing.Playwright.Options;
using Granit.Browsing.Pool;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Browsing.Playwright.Extensions;

/// <summary>DI extensions for <c>Granit.Browsing.Playwright</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Microsoft.Playwright provider for <see cref="IHeadlessBrowser"/> +
    /// the capability set advertised by the chosen engine. Capability registrations are
    /// gated on <see cref="PlaywrightOptions.Engine"/> — Firefox / WebKit do not register
    /// <see cref="IPdfCapability"/> or <see cref="IPdfViewerCapability"/>; resolution
    /// fails at boot with a "service not registered" error in that case.
    /// </summary>
    /// <remarks>
    /// A single host registers exactly one Granit.Browsing provider — calling both this
    /// extension and <c>AddGranitBrowsingPuppeteerSharp</c> in the same DI container is
    /// not supported (the second registration is silently dropped by <c>TryAdd</c>).
    /// </remarks>
    public static IServiceCollection AddGranitBrowsingPlaywright(
        this IServiceCollection services,
        Action<GranitBrowsingOptions>? configureBrowsing = null,
        Action<PlaywrightOptions>? configurePlaywright = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<GranitBrowsingOptions>()
            .BindConfiguration(GranitBrowsingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<PlaywrightOptions>()
            .BindConfiguration(PlaywrightOptions.SectionName)
            .ValidateOnStart();

        if (configureBrowsing is not null)
        {
            services.PostConfigure(configureBrowsing);
        }
        if (configurePlaywright is not null)
        {
            services.PostConfigure(configurePlaywright);
        }

        services.TryAddSingleton<PlaywrightHeadlessBrowser>();
        services.TryAddSingleton<TenantAwareHeadlessBrowser>(sp =>
            ActivatorUtilities.CreateInstance<TenantAwareHeadlessBrowser>(
                sp,
                sp.GetRequiredService<PlaywrightHeadlessBrowser>()));

        services.TryAddSingleton<IHeadlessBrowser>(sp => sp.GetRequiredService<TenantAwareHeadlessBrowser>());
        services.TryAddSingleton<IHeadlessBrowserPool>(sp => sp.GetRequiredService<PlaywrightHeadlessBrowser>());

        // Capabilities advertised on every engine. AccessibilityTree intentionally absent —
        // Playwright .NET deprecated the tree-export API in favour of ARIA snapshots.
        services.TryAddSingleton<Granit.Browsing.Pages.IHarScrubber, Granit.Browsing.Pages.DefaultHarScrubber>();
        services.TryAddSingleton<ITracingCapability, PlaywrightTracingCapability>();
        services.TryAddSingleton<IHarRecordingCapability, PlaywrightHarRecordingCapability>();

        // Chromium-only capabilities — registered with a guard factory so that resolving
        // them under a Firefox / WebKit engine surfaces a descriptive error instead of
        // succeeding with a runtime-unsupported instance. Use TryAddSingleton so a host
        // that pre-registered a custom impl wins.
        services.TryAddSingleton<IPdfCapability>(sp =>
        {
            PlaywrightOptions opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PlaywrightOptions>>().Value;
            if (opts.Engine != BrowserEngine.Chromium)
            {
                throw new InvalidOperationException(
                    $"IPdfCapability is only available with the Chromium engine; configured engine: {opts.Engine}. " +
                    "Switch PlaywrightOptions.Engine to Chromium or use the PuppeteerSharp provider.");
            }
            return new PlaywrightPdfCapability(
                sp.GetRequiredService<Granit.Browsing.Diagnostics.BrowsingMetrics>(),
                sp.GetService<Granit.MultiTenancy.ICurrentTenant>());
        });
        services.TryAddSingleton<IPdfViewerCapability>(sp =>
        {
            PlaywrightOptions opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PlaywrightOptions>>().Value;
            if (opts.Engine != BrowserEngine.Chromium)
            {
                throw new InvalidOperationException(
                    $"IPdfViewerCapability is only available with the Chromium engine; configured engine: {opts.Engine}. " +
                    "Switch PlaywrightOptions.Engine to Chromium or use the PuppeteerSharp provider.");
            }
            return ActivatorUtilities.CreateInstance<PlaywrightPdfViewerCapability>(sp);
        });

        services.AddHostedService<PlaywrightProvisionService>();

        return services;
    }
}
