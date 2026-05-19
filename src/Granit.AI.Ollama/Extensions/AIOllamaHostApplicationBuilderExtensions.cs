using System.Diagnostics.CodeAnalysis;
using Granit.AI.Ollama.Diagnostics;
using Granit.AI.Ollama.HealthChecks;
using Granit.AI.Ollama.Internal;
using Granit.AI.Ollama.Options;
using Granit.Diagnostics;
using Granit.Http.Resilience.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.AI.Ollama.Extensions;

/// <summary>
/// Extension methods for registering the Ollama AI provider.
/// </summary>
// DI wiring only — no logic to unit test.
[ExcludeFromCodeCoverage]
public static class AIOllamaHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds <c>Granit.AI.Ollama</c> services: Ollama provider factory, options, and activity source.
    /// </summary>
    /// <remarks>
    /// Reads <see cref="OllamaProviderOptions"/> from the <c>"AI:Ollama"</c> configuration section.
    /// No API key is required — Ollama runs models locally.
    /// Registers the named <see cref="HttpClient"/> consumed by both the SDK and the health check.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitAIOllama(
        this IHostApplicationBuilder builder)
    {
        GranitActivitySourceRegistry.Register(AIOllamaActivitySource.Name);

        builder.Services
            .AddOptions<OllamaProviderOptions>()
            .BindConfiguration(OllamaProviderOptions.SectionName)
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<OllamaProviderOptions>, OllamaProviderOptionsValidator>();

        // TimeProvider may already be registered by the host; TryAdd avoids overriding it.
        builder.Services.TryAddSingleton(TimeProvider.System);

        // The OllamaSharp client uses the HttpClient timeout directly (no separate SDK timeout).
        builder.Services.AddHttpClient(OllamaProviderFactory.HttpClientName);

        builder.Services.AddSingleton<IAIProviderFactory, OllamaProviderFactory>();

        builder.Services.AddGranitHttpClient("GranitAIOllamaHealthCheck");

        return builder;
    }

    /// <summary>
    /// Adds an Ollama connectivity health check tagged <c>"readiness"</c> and <c>"startup"</c>.
    /// Verifies that the Ollama server is reachable by issuing a <c>GET /api/tags</c> request.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">Check name. Defaults to <c>"ollama"</c>.</param>
    /// <param name="failureStatus">Status on failure. Defaults to <see cref="HealthStatus.Unhealthy"/>.</param>
    /// <param name="timeout">Check timeout. Defaults to 10 seconds.</param>
    /// <returns>The health checks builder for chaining.</returns>
    public static IHealthChecksBuilder AddGranitOllamaHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "ollama",
        HealthStatus? failureStatus = null,
        TimeSpan? timeout = null)
    {
        builder.Services.AddSingleton<OllamaHealthCheck>();

        return builder.Add(new HealthCheckRegistration(
            name,
            sp => sp.GetRequiredService<OllamaHealthCheck>(),
            failureStatus,
            ["readiness", "startup"],
            timeout ?? TimeSpan.FromSeconds(10)));
    }
}
