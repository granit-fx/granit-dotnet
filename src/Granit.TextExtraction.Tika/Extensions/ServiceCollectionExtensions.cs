using Granit.TextExtraction.Extensions;
using Granit.TextExtraction.Tika.Internal;
using Granit.TextExtraction.Tika.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Granit.TextExtraction.Tika.Extensions;

/// <summary>
/// Extension methods for registering the Tika sidecar text extractor.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="TikaSidecarTextExtractor"/> with the
    /// <c>Granit.TextExtraction</c> pipeline. Implicitly calls
    /// <c>AddGranitTextExtraction()</c>, binds <see cref="TikaSidecarOptions"/>, and
    /// registers a named <see cref="HttpClient"/> under
    /// <see cref="TikaSidecarTextExtractor.HttpClientName"/>. The host MAY chain
    /// <c>.ConfigurePrimaryHttpMessageHandler(...)</c> on the returned
    /// <see cref="IHttpClientBuilder"/> to attach a client certificate / mTLS handler —
    /// when <see cref="TikaSidecarOptions.RequireMutualTls"/> is <c>true</c> (the default),
    /// doing so is mandatory and is enforced at startup.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration callback that runs after binding
    /// from <see cref="TikaSidecarOptions.SectionName"/>.</param>
    /// <returns>
    /// The <see cref="IHttpClientBuilder"/> for the <c>granit-tika</c> named client so
    /// the host can chain mTLS / resilience policy / metric handlers.
    /// </returns>
    public static IHttpClientBuilder AddTikaSidecarExtractor(
        this IServiceCollection services,
        Action<TikaSidecarOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGranitTextExtraction();
        services.AddTextExtractor<TikaSidecarTextExtractor>();

        services
            .AddOptions<TikaSidecarOptions>()
            .BindConfiguration(TikaSidecarOptions.SectionName)
            .Validate(ValidateAllowedHosts, "TikaSidecarOptions.AllowedHosts must not be empty.")
            .Validate(ValidateUriHostInAllowlist,
                "TikaSidecarOptions.Uri host must appear in AllowedHosts.")
            .Validate(ValidateScheme,
                "TikaSidecarOptions.Uri must use https:// when RequireHttps is true " +
                "(http:// is only accepted for localhost).")
            .ValidateOnStart();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        // VULN-102: enforce RequireMutualTls at startup by inspecting the named client's
        // HttpClientFactoryOptions handler-chain configuration. The post-configurator throws
        // on first resolution of the named client when RequireMutualTls=true and no host
        // handler has been wired through .ConfigurePrimaryHttpMessageHandler(...).
        services.AddSingleton<IPostConfigureOptions<HttpClientFactoryOptions>,
            TikaMutualTlsHandlerPostConfigurer>();

        return services.AddHttpClient(TikaSidecarTextExtractor.HttpClientName);
    }

    private static bool ValidateAllowedHosts(TikaSidecarOptions options) =>
        options.AllowedHosts.Count > 0;

    private static bool ValidateUriHostInAllowlist(TikaSidecarOptions options)
    {
        if (options.Uri is null || options.AllowedHosts.Count == 0)
        {
            // Other validators report these as their own failures.
            return true;
        }

        return options.AllowedHosts.Any(host =>
            string.Equals(host, options.Uri.Host, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ValidateScheme(TikaSidecarOptions options)
    {
        if (options.Uri is null || !options.RequireHttps)
        {
            return true;
        }

        if (string.Equals(options.Uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Localhost escape hatch — keeps `dotnet run` against a local apache/tika container
        // frictionless while still pinning prod / staging to HTTPS.
        return options.Uri.IsLoopback;
    }
}
