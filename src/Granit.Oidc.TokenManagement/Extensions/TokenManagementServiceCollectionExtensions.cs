using Granit.Diagnostics;
using Granit.Http.Resilience.Extensions;
using Granit.Oidc.TokenManagement.Cache;
using Granit.Oidc.TokenManagement.Cache.Internal;
using Granit.Oidc.TokenManagement.Diagnostics;
using Granit.Oidc.TokenManagement.Handlers;
using Granit.Oidc.TokenManagement.Options;
using Granit.Oidc.TokenManagement.Services;
using Granit.Oidc.TokenManagement.Services.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Granit.Oidc.TokenManagement.Extensions;

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

        services.AddGranitHttpClient("Granit.TokenManagement");

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

        return services.AddGranitHttpClient(name)
            .AddHttpMessageHandler(sp =>
            {
                ClientCredentialsTokenHandler handler = ActivatorUtilities.CreateInstance<ClientCredentialsTokenHandler>(sp);
                handler.ClientName = name;
                return handler;
            });
    }

    /// <summary>
    /// Registers a named HTTP client that performs OAuth 2.0 Token Exchange
    /// (RFC 8693) on the caller's inbound access token and attaches the
    /// audience-scoped result to every outbound request. Optionally binds
    /// the exchanged token with DPoP (RFC 9449) — enabled by default.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The logical name for the HTTP client and its options.</param>
    /// <param name="configure">Delegate configuring the <see cref="OnBehalfOfOptions"/>.</param>
    /// <returns>An <see cref="IHttpClientBuilder"/> for further HTTP client configuration.</returns>
    /// <remarks>
    /// <para>
    /// <b>Why not a simpler bearer-token propagation?</b> Copying the inbound
    /// <c>Authorization</c> header to downstream requests is a confused
    /// deputy: the token carries the caller's full audience and scopes, so a
    /// compromised or attacker-influenced downstream target can replay it
    /// against the upstream API. Token exchange issues a new token whose
    /// <c>aud</c> claim is narrowed to <see cref="OnBehalfOfOptions.Audience"/>,
    /// neutralising that vector. Combined with DPoP, a stolen token is also
    /// unusable without the per-process proof key.
    /// </para>
    /// <para>
    /// The handler requires <see cref="OnBehalfOfOptions.AllowedHosts"/> to
    /// be populated and (by default) rejects non-https targets.
    /// </para>
    /// </remarks>
    public static IHttpClientBuilder AddOnBehalfOfHttpClient(
        this IServiceCollection services,
        string name,
        Action<OnBehalfOfOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddGranitTokenManagement();
        services.TryAddSingleton<IValidateOptions<OnBehalfOfOptions>, OnBehalfOfOptionsValidator>();
        services.Configure(name, configure);
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        return services.AddGranitHttpClient(name)
            .AddHttpMessageHandler(sp =>
            {
                OnBehalfOfTokenHandler handler = ActivatorUtilities.CreateInstance<OnBehalfOfTokenHandler>(sp);
                handler.ClientName = name;
                return handler;
            });
    }
}
