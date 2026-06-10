using System.Diagnostics.CodeAnalysis;
using Granit.AI.Ollama.Diagnostics;
using Granit.AI.Ollama.Handlers;
using Granit.AI.Ollama.HealthChecks;
using Granit.AI.Ollama.Internal;
using Granit.AI.Ollama.Options;
using Granit.AI.Tenancy;
using Granit.Diagnostics;
using Granit.Events;
using Granit.Http.Resilience.Extensions;
using Granit.Settings.Events;
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

        // Two named clients keep the connect-time policy aligned with the scope-validated URL.
        // Tenant/Workspace/Global endpoints connect via the strict OllamaTenant policy (no
        // private IPs — DNS rebinding defence). The Host-options endpoint connects via the
        // permissive policy so an operator-trusted private deployment is reachable. Both
        // disable redirects to close the redirect-to-metadata SSRF path.
        builder.Services
            .AddHttpClient(OllamaProviderFactory.TenantHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectCallback = GranitSafeConnectCallback.Create(AIEndpointPolicy.OllamaTenant),
            });

        builder.Services
            .AddHttpClient(OllamaProviderFactory.HostHttpClientName)
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ConnectCallback = GranitSafeConnectCallback.Create(AIEndpointPolicy.HostPermissive),
            });

        builder.Services.AddSingleton<OllamaClientCache>();
        builder.Services.AddScoped<OllamaCredentialResolver>();
        builder.Services.AddScoped<IAIProviderCredentialResolver>(sp =>
            sp.GetRequiredService<OllamaCredentialResolver>());
        builder.Services.AddScoped<IAIProviderFactory, OllamaProviderFactory>();

        // Evict cached SDK clients the moment an Ollama endpoint setting changes,
        // rather than waiting out the cache's 90s sliding expiration.
        builder.Services.AddScoped<ILocalEventHandler<SettingChangedEvent>, OllamaCredentialCacheInvalidationHandler>();

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
