using Granit.Http.Resilience.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

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
    /// <c>appsettings.json</c> without redeploying.
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

        // Per-client override: HttpResilience:{name}:* → HttpStandardResilienceOptions.
        // AddStandardResilienceHandler uses named options key "{name}-standard".
        // IPostConfigureOptions runs after all IConfigureOptions, guaranteeing user
        // settings override the pipeline defaults.
        services.AddTransient<IPostConfigureOptions<HttpStandardResilienceOptions>>(sp =>
        {
            IConfiguration config = sp.GetRequiredService<IConfiguration>();
            return new PostConfigureOptions<HttpStandardResilienceOptions>(
                $"{name}-standard",
                opts => config
                    .GetSection($"{HttpResilienceOptions.SectionName}:{name}")
                    .Bind(opts));
        });

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
}
