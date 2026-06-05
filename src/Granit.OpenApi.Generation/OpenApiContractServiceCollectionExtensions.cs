using Microsoft.Extensions.DependencyInjection;

namespace Granit.OpenApi.Generation;

/// <summary>Service-collection helpers for build-time OpenAPI contract generation.</summary>
public static class OpenApiContractServiceCollectionExtensions
{
    /// <summary>
    /// Registers each contract as an inert <c>null</c> singleton so the minimal-API binder classifies it
    /// as a service (not a request body) during doc-gen. Use sparingly, for application-service
    /// parameters a contract-only generator cannot compose and that are <b>not</b> annotated with
    /// <c>[FromServices]</c> — the proper fix is the attribute, which makes this unnecessary. The stubs
    /// are never resolved (handlers do not run during generation), so <c>null</c> is safe and the
    /// explicit list documents exactly which services leak through the endpoints layer.
    /// </summary>
    /// <param name="services">The doc-gen service collection.</param>
    /// <param name="contracts">The application-service contract types to stub.</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddContractServiceStubs(
        this IServiceCollection services,
        params ReadOnlySpan<Type> contracts)
    {
        ArgumentNullException.ThrowIfNull(services);

        foreach (Type contract in contracts)
        {
            ArgumentNullException.ThrowIfNull(contract);
            services.AddSingleton(contract, _ => null!);
        }

        return services;
    }
}
