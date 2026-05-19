using Granit.Browsing.Capabilities;
using Granit.Browsing.Options;
using Granit.Browsing.Pool;
using Granit.Browsing.PuppeteerSharp.Internal;
using Granit.Browsing.PuppeteerSharp.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Browsing.PuppeteerSharp.Extensions;

/// <summary>DI extension methods for <c>Granit.Browsing.PuppeteerSharp</c>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the PuppeteerSharp provider for <see cref="IHeadlessBrowser"/> + advertised
    /// capabilities (<see cref="IPdfCapability"/>, <see cref="IPdfViewerCapability"/>,
    /// <see cref="IAccessibilityCapability"/>). Also wires the first-run Chromium
    /// provisioning service.
    /// </summary>
    /// <remarks>
    /// A single host registers exactly one Granit.Browsing provider — calling both this
    /// extension and <c>AddGranitBrowsingPlaywright</c> in the same DI container is not
    /// supported (the second registration is silently dropped by <c>TryAdd</c>).
    /// </remarks>
    /// <param name="services">DI service collection.</param>
    /// <param name="configureBrowsing">Optional override for the engine-agnostic <see cref="GranitBrowsingOptions"/>.</param>
    /// <param name="configurePuppeteer">Optional override for the PuppeteerSharp-specific <see cref="PuppeteerSharpOptions"/>.</param>
    public static IServiceCollection AddGranitBrowsingPuppeteerSharp(
        this IServiceCollection services,
        Action<GranitBrowsingOptions>? configureBrowsing = null,
        Action<PuppeteerSharpOptions>? configurePuppeteer = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddOptions<GranitBrowsingOptions>()
            .BindConfiguration(GranitBrowsingOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<PuppeteerSharpOptions>()
            .BindConfiguration(PuppeteerSharpOptions.SectionName)
            .ValidateOnStart();

        if (configureBrowsing is not null)
        {
            services.PostConfigure(configureBrowsing);
        }
        if (configurePuppeteer is not null)
        {
            services.PostConfigure(configurePuppeteer);
        }

        services.TryAddSingleton<PuppeteerHeadlessBrowser>();
        services.TryAddSingleton<TenantAwareHeadlessBrowser>(sp =>
            ActivatorUtilities.CreateInstance<TenantAwareHeadlessBrowser>(
                sp,
                sp.GetRequiredService<PuppeteerHeadlessBrowser>()));

        services.TryAddSingleton<IHeadlessBrowser>(sp => sp.GetRequiredService<TenantAwareHeadlessBrowser>());
        services.TryAddSingleton<IHeadlessBrowserPool>(sp => sp.GetRequiredService<PuppeteerHeadlessBrowser>());

        services.TryAddSingleton<IPdfCapability, PuppeteerPdfCapability>();
        services.TryAddSingleton<IPdfViewerCapability, PuppeteerPdfViewerCapability>();
        services.TryAddSingleton<IAccessibilityCapability, PuppeteerAccessibilityCapability>();

        services.TryAddSingleton<PuppeteerBrowserFetcherPolicy>();
        services.AddHostedService<PuppeteerChromiumProvisionService>();

        return services;
    }
}
