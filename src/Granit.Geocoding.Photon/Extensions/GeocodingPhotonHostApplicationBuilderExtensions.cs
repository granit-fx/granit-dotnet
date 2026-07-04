using System.Diagnostics.CodeAnalysis;
using Granit.Geocoding.Internal;
using Granit.Geocoding.Photon.Internal;
using Granit.Geocoding.Photon.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Geocoding.Photon.Extensions;

/// <summary>
/// Extension methods for registering the opt-in Photon (<c>photon.komoot.io</c>) geocoding provider.
/// </summary>
// DI wiring only — no logic to unit test.
[ExcludeFromCodeCoverage]
public static class GeocodingPhotonHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Photon geocoding provider as an <see cref="IGeocodingProvider"/>.
    /// </summary>
    /// <remarks>
    /// Binds <see cref="PhotonGeocodingOptions"/> from <c>"Geocoding:Photon"</c> and validates it on start.
    /// <strong>GDPR:</strong> this transmits the address to an external service — only call this when that data flow
    /// is approved, and add the configured <c>ProviderName</c> (default <c>"Photon"</c>) to
    /// <c>Geocoding:ProviderOrder</c>. The primary handler disables auto-redirects to close the redirect-based SSRF
    /// path, the default request logging is removed, and a redaction handler strips the address from the outbound
    /// trace span.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitGeocodingPhoton(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddOptions<PhotonGeocodingOptions>()
            .BindConfiguration(PhotonGeocodingOptions.SectionName)
            .ValidateOnStart();

        builder.Services
            .AddSingleton<IValidateOptions<PhotonGeocodingOptions>, PhotonGeocodingOptionsValidator>();

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddTransient<AddressTelemetryRedactionHandler>();

        builder.Services
            .AddHttpClient(PhotonGeocodingProvider.HttpClientName, static (sp, client) =>
            {
                PhotonGeocodingOptions options =
                    sp.GetRequiredService<IOptions<PhotonGeocodingOptions>>().Value;
                client.BaseAddress = options.BaseAddress;
                client.Timeout = options.Timeout;
                client.MaxResponseContentBufferSize = options.MaxResponseSizeBytes;

                // Photon does not mandate a User-Agent (unlike Nominatim); send one only when configured.
                if (!string.IsNullOrWhiteSpace(options.UserAgent))
                {
                    client.DefaultRequestHeaders.Add("User-Agent", options.UserAgent);
                }
            })
            // The default HttpClientFactory logging handlers record the request URI at Information level — and for
            // this client the URI query string carries the address (personal data under GDPR). The redaction handler
            // scrubs the trace span but not this parallel ILogger channel, so strip the default loggers entirely;
            // failures are already logged (category only) by the provider.
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
            })
            .AddHttpMessageHandler<AddressTelemetryRedactionHandler>();

        // Register ONE shared instance for both capabilities so forward and reverse calls pace through the same
        // rate-limit throttle (separate instances would each get their own gate and could exceed the policy).
        builder.Services.AddSingleton<PhotonGeocodingProvider>();
        builder.Services.AddSingleton<IGeocodingProvider>(
            static sp => sp.GetRequiredService<PhotonGeocodingProvider>());
        builder.Services.AddSingleton<IReverseGeocodingProvider>(
            static sp => sp.GetRequiredService<PhotonGeocodingProvider>());
        builder.Services.AddSingleton<IAddressAutocompleteProvider>(
            static sp => sp.GetRequiredService<PhotonGeocodingProvider>());

        return builder;
    }
}
