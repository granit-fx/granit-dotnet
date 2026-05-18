using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.MultiTenancy.Authorization.Extensions;

/// <summary>
/// DI extensions for <c>Granit.MultiTenancy.Authorization</c>.
/// </summary>
public static class MultiTenancyAuthorizationServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the default <see cref="IHostImpersonationGate"/> (deny-all) with
    /// <see cref="PermissionBasedHostImpersonationGate"/>, which delegates to
    /// <c>IPermissionChecker</c>. Opt-in — apps must explicitly call this to enable
    /// Host-side tenant impersonation via the <c>MultiTenancy.Host.Impersonate</c>
    /// permission.
    /// </summary>
    public static IServiceCollection AddGranitHostImpersonationWithPermissions(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Replace(ServiceDescriptor.Scoped<IHostImpersonationGate, PermissionBasedHostImpersonationGate>());
        return services;
    }
}
