using Granit.Identity.Federated.Internal;
using Granit.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Granit.Identity.Federated.Extensions;

/// <summary>
/// Wires the <see cref="GracefulIdentityProviderDecorator"/> around a concrete federated
/// identity provider.
/// </summary>
public static class IdentityProviderDegradationExtensions
{
    /// <summary>
    /// Wraps the registered <see cref="IIdentityProvider"/> (expected to be
    /// <typeparamref name="TProvider"/>) with the graceful-degradation decorator. Call this
    /// immediately after <c>AddIdentityProvider&lt;TProvider&gt;()</c> in a provider's
    /// registration extension.
    /// </summary>
    /// <remarks>
    /// The concrete <typeparamref name="TProvider"/> is registered by its own type so that
    /// (a) the decorator can resolve and wrap it, and (b) multi-facet forwarders that need the
    /// raw provider (the session/device facets, which the decorator does not implement) resolve
    /// it directly rather than the decorator. Fine-grained <c>IIdentity*</c> facet registrations
    /// forward to <see cref="IIdentityProvider"/> and therefore inherit the decoration.
    /// </remarks>
    public static IServiceCollection DecorateIdentityProviderWithGracefulDegradation<TProvider>(
        this IServiceCollection services)
        where TProvider : class, IIdentityProvider
    {
        // Concrete provider resolvable by type (for the decorator + session/device forwarders).
        services.TryAddScoped<TProvider>();

        services.Replace(ServiceDescriptor.Scoped<IIdentityProvider>(sp =>
            new GracefulIdentityProviderDecorator(
                sp.GetRequiredService<TProvider>(),
                sp.GetRequiredService<ILogger<GracefulIdentityProviderDecorator>>(),
                sp.GetService<Diagnostics.IdentityFederatedMetrics>(),
                sp.GetService<ICurrentTenant>())));

        return services;
    }
}
