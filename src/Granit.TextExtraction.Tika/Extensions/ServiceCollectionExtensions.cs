using Granit.TextExtraction.Extensions;
using Granit.TextExtraction.Tika.Options;
using Microsoft.Extensions.DependencyInjection;
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
    /// <see cref="IHttpClientBuilder"/> to attach a client certificate / mTLS handler.
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

        OptionsBuilder<TikaSidecarOptions> optionsBuilder = services
            .AddOptions<TikaSidecarOptions>()
            .BindConfiguration(TikaSidecarOptions.SectionName)
            .Validate(ValidateAllowedHosts, "TikaSidecarOptions.AllowedHosts must not be empty.")
            .Validate(ValidateUriHostInAllowlist,
                "TikaSidecarOptions.Uri host must appear in AllowedHosts.")
            .ValidateOnStart();

        if (configure is not null)
        {
            services.Configure(configure);
        }

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
}
