using Granit.Http.Resilience.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace Granit.Http.Resilience.Extensions;

/// <summary>
/// Extension methods for registering resilient named <see cref="HttpClient"/> instances.
/// </summary>
public static class HttpResilienceServiceCollectionExtensions
{
    /// <summary>
    /// Adds a named <see cref="HttpClient"/> with the standard Granit resilience pipeline applied:
    /// retry with exponential back-off, circuit breaker, and per-request timeout.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The logical name of the <see cref="HttpClient"/>.</param>
    /// <param name="configure">Optional per-client configuration callback.</param>
    /// <returns>The <see cref="IHttpClientBuilder"/> for further chaining.</returns>
    /// <remarks>
    /// Per-client pipeline settings are read at runtime from the
    /// <c>HttpResilience:{name}</c> configuration section, allowing operators to tune
    /// retry counts, circuit-breaker thresholds, and timeouts per named client via
    /// <c>appsettings.json</c> without redeploying. Runtime telemetry (retry / circuit-breaker /
    /// timeout events) is emitted by the upstream <c>Polly</c> meter shipped with
    /// <c>Microsoft.Extensions.Http.Resilience</c>; enable it in OpenTelemetry by adding
    /// the meter name <c>"Polly"</c>.
    /// <para>
    /// <b>SSRF posture.</b> This pipeline does <i>not</i> validate outbound destinations:
    /// it will faithfully retry whatever <c>RequestUri</c> the caller supplies. When the
    /// request URI is influenced by untrusted input, compose this client with
    /// <c>Granit.Http.Security</c>'s SSRF protections (allowlist resolver + private-network
    /// guard) so retries cannot amplify probes against internal infrastructure.
    /// </para>
    /// </remarks>
    public static IHttpClientBuilder AddGranitHttpClient(
        this IServiceCollection services,
        string name,
        Action<IServiceProvider, HttpClient>? configure = null)
    {
        IHttpClientBuilder clientBuilder = configure is null
            ? services.AddHttpClient(name)
            : services.AddHttpClient(name, configure);

        clientBuilder.AddStandardResilienceHandler();

        // AddStandardResilienceHandler registers its defaults under the named options key
        // "{name}-standard". Binding from configuration AFTER that registration ensures
        // user-supplied values in HttpResilience:{name} override the framework defaults.
        services
            .AddOptions<HttpStandardResilienceOptions>($"{name}-standard")
            .BindConfiguration($"{HttpResilienceOptions.SectionName}:{name}");

        return clientBuilder;
    }

    /// <summary>
    /// Adds a named <see cref="HttpClient"/> with the standard Granit resilience pipeline applied.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The logical name of the <see cref="HttpClient"/>.</param>
    /// <param name="configure">Per-client configuration callback.</param>
    /// <returns>The <see cref="IHttpClientBuilder"/> for further chaining.</returns>
    public static IHttpClientBuilder AddGranitHttpClient(
        this IServiceCollection services,
        string name,
        Action<HttpClient> configure)
        => services.AddGranitHttpClient(name, (_, client) => configure(client));

    // NOTE — AddAuthTokenPropagation was removed as part of the confused-deputy hardening.
    // The handler blindly copied the inbound Authorization header to every
    // outbound host, which is a confused-deputy vulnerability (OAuth2 Security
    // BCP §4.8). Use `AddOnBehalfOfHttpClient` from Granit.Oidc.TokenManagement
    // instead, which performs RFC 8693 token exchange to a narrowed audience
    // and optionally binds the exchanged token with DPoP (RFC 9449).
}
