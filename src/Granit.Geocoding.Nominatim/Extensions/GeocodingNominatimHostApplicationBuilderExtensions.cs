using System.Diagnostics.CodeAnalysis;
using Granit.Geocoding.Internal;
using Granit.Geocoding.Nominatim.Internal;
using Granit.Geocoding.Nominatim.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Geocoding.Nominatim.Extensions;

/// <summary>
/// Extension methods for registering the opt-in OpenStreetMap Nominatim geocoding provider.
/// </summary>
// DI wiring only — no logic to unit test.
[ExcludeFromCodeCoverage]
public static class GeocodingNominatimHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Nominatim geocoding provider as an <see cref="IGeocodingProvider"/>.
    /// </summary>
    /// <remarks>
    /// Binds <see cref="NominatimGeocodingOptions"/> from <c>"Geocoding:Nominatim"</c> and validates it on start.
    /// <strong>GDPR:</strong> this transmits the address to an external service — only call this when that data flow
    /// is approved, and add the configured <c>ProviderName</c> (default <c>"Nominatim"</c>) to
    /// <c>Geocoding:ProviderOrder</c>. The primary handler disables auto-redirects to close the redirect-based SSRF
    /// path, the default request logging is removed, and a redaction handler strips the address from the outbound
    /// trace span.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitGeocodingNominatim(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddOptions<NominatimGeocodingOptions>()
            .BindConfiguration(NominatimGeocodingOptions.SectionName)
            .ValidateOnStart();

        builder.Services
            .AddSingleton<IValidateOptions<NominatimGeocodingOptions>, NominatimGeocodingOptionsValidator>();

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddTransient<AddressTelemetryRedactionHandler>();

        builder.Services
            .AddHttpClient(NominatimGeocodingProvider.HttpClientName, static (sp, client) =>
            {
                NominatimGeocodingOptions options =
                    sp.GetRequiredService<IOptions<NominatimGeocodingOptions>>().Value;
                client.BaseAddress = options.BaseAddress;
                client.Timeout = options.Timeout;
                client.MaxResponseContentBufferSize = options.MaxResponseSizeBytes;

                // Mandatory under the Nominatim usage policy; validation guarantees it is non-empty.
                client.DefaultRequestHeaders.Add("User-Agent", options.UserAgent);
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
        builder.Services.AddSingleton<NominatimGeocodingProvider>();
        builder.Services.AddSingleton<IGeocodingProvider>(
            static sp => sp.GetRequiredService<NominatimGeocodingProvider>());
        builder.Services.AddSingleton<IReverseGeocodingProvider>(
            static sp => sp.GetRequiredService<NominatimGeocodingProvider>());

        return builder;
    }
}
