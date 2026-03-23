using Granit.Authentication.TokenManagement.Cache;
using Granit.Authentication.TokenManagement.Cache.Internal;
using Granit.Authentication.TokenManagement.Diagnostics;
using Granit.Authentication.TokenManagement.Handlers;
using Granit.Authentication.TokenManagement.Options;
using Granit.Authentication.TokenManagement.Services;
using Granit.Authentication.TokenManagement.Services.Internal;
using Granit.Core.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Authentication.TokenManagement.Extensions;

/// <summary>
/// Extension methods for registering token management services with the DI container.
/// </summary>
public static class TokenManagementServiceCollectionExtensions
{
    /// <summary>
    /// Registers token management core services: <see cref="ITokenEndpointService"/>,
    /// <see cref="ITokenRevocationService"/>, <see cref="IClientCredentialsTokenCache"/>,
    /// metrics, and the internal named HTTP client.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddGranitTokenManagement(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<TokenManagementMetrics>();
        services.TryAddSingleton<ITokenEndpointService, TokenEndpointService>();
        services.TryAddSingleton<ITokenRevocationService, TokenRevocationService>();
        services.TryAddSingleton<IClientCredentialsTokenCache, ClientCredentialsTokenCache>();

        services.AddHttpClient("Granit.TokenManagement");

        GranitActivitySourceRegistry.Register(TokenManagementActivitySource.Name);

        return services;
    }

    /// <summary>
    /// Registers a named HTTP client that automatically acquires and attaches client credentials
    /// tokens to outbound requests. The client is configured via <paramref name="configure"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The logical name for the HTTP client and its associated options.</param>
    /// <param name="configure">A delegate to configure the <see cref="ClientCredentialsOptions"/>.</param>
    /// <returns>An <see cref="IHttpClientBuilder"/> for further HTTP client configuration.</returns>
    public static IHttpClientBuilder AddClientCredentialsHttpClient(
        this IServiceCollection services,
        string name,
        Action<ClientCredentialsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddGranitTokenManagement();
        services.Configure(name, configure);

        return services.AddHttpClient(name)
            .AddHttpMessageHandler(sp =>
            {
                ClientCredentialsTokenHandler handler = ActivatorUtilities.CreateInstance<ClientCredentialsTokenHandler>(sp);
                handler.ClientName = name;
                return handler;
            });
    }
}
