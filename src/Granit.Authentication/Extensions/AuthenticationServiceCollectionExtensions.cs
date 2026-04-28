using Granit.Authentication.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Authentication.Extensions;

/// <summary>
/// Service registration helpers for cross-scheme authentication primitives.
/// </summary>
public static class AuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="OpenIddictRoleClaimsTransformation"/> as a scoped
    /// <see cref="IClaimsTransformation"/>. Idempotent: safe to call from both
    /// <c>Granit.OpenIddict.Server.AddGranitOpenIddictServer</c> and
    /// <c>Granit.Authentication.OpenIddict.AddGranitOpenIddictAuthentication</c> —
    /// only one descriptor is registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitOpenIddictRoleClaimNormalization(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IClaimsTransformation, OpenIddictRoleClaimsTransformation>());

        return services;
    }
}
