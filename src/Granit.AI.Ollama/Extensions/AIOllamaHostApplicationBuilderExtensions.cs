using System.Diagnostics.CodeAnalysis;
using Granit.AI.Ollama.Diagnostics;
using Granit.AI.Ollama.HealthChecks;
using Granit.AI.Ollama.Internal;
using Granit.AI.Ollama.Options;
using Granit.AI.Tenancy;
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
    /// Adds <c>Granit.AI.Ollama</c> services: Ollama provider factory, options, activity source,
    /// credential resolver + cache.
    /// </summary>
    /// <remarks>
    /// Endpoint cascade Workspace → Tenant Setting → Global Setting → Host Options enforced by
    /// <see cref="OllamaCredentialResolver"/>. Tenant/Workspace endpoints validated against
    /// <see cref="AIEndpointPolicy.OllamaTenant"/>; the Host Options endpoint uses
    /// <see cref="AIEndpointPolicy.HostPermissive"/> (operator-trusted).
    /// </remarks>
    public static IHostApplicationBuilder AddGranitAIOllama(this IHostApplicationBuilder builder)
    {
        GranitActivitySourceRegistry.Register(AIOllamaActivitySource.Name);

        builder.Services
            .AddOptions<OllamaProviderOptions>()
            .BindConfiguration(OllamaProviderOptions.SectionName)
            .ValidateOnStart();

        builder.Services.AddSingleton<IValidateOptions<OllamaProviderOptions>, OllamaProviderOptionsValidator>();

        builder.Services.TryAddSingleton(TimeProvider.System);

        // The OllamaSharp client uses the HttpClient timeout directly. AllowAutoRedirect=false +
        // GranitSafeConnectCallback close the redirect-to-metadata SSRF path even when an
        // operator points the host at a permissive endpoint.
        builder.Services
            .AddHttpClient(OllamaProviderFactory.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectCallback = GranitSafeConnectCallback.Create(AIEndpointPolicy.OllamaTenant),
            });

        builder.Services.AddSingleton<OllamaClientCache>();
        builder.Services.AddScoped<OllamaCredentialResolver>();
        builder.Services.AddScoped<IAIProviderCredentialResolver>(sp =>
            sp.GetRequiredService<OllamaCredentialResolver>());
        builder.Services.AddScoped<IAIProviderFactory, OllamaProviderFactory>();

        builder.Services.AddGranitHttpClient("GranitAIOllamaHealthCheck");

        return builder;
    }

    /// <summary>
    /// Adds an Ollama connectivity health check tagged <c>"readiness"</c> and <c>"startup"</c>.
    /// </summary>
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
