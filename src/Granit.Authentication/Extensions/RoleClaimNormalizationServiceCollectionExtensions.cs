using Granit.Authentication.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Authentication.Extensions;

/// <summary>
/// Service registration helpers for cross-scheme role-claim normalization.
/// </summary>
public static class RoleClaimNormalizationServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="RoleClaimNormalizationTransformation"/> as a scoped
    /// <see cref="IClaimsTransformation"/> and applies the supplied configuration to
    /// <see cref="RoleClaimNormalizationOptions"/>. Multiple calls are additive on
    /// <see cref="RoleClaimNormalizationOptions.Schemes"/>: each consumer (e.g.
    /// <c>Granit.Authentication.OpenIddict</c>, <c>Granit.OpenIddict.Server</c>) can
    /// add its own scheme without overwriting the others. The
    /// <see cref="IClaimsTransformation"/> registration itself uses
    /// <see cref="ServiceCollectionDescriptorExtensions.TryAddEnumerable(IServiceCollection, ServiceDescriptor)"/>
    /// so duplicate registrations are harmless.
    /// </summary>
    public static IServiceCollection AddGranitRoleClaimNormalization(
        this IServiceCollection services,
        Action<RoleClaimNormalizationOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.Configure(configure);
        services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IClaimsTransformation, RoleClaimNormalizationTransformation>());

        return services;
    }
}
