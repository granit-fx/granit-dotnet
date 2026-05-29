using Granit.Identity.Notifications.Internal;
using Granit.Identity.Notifications.Options;
using Granit.Notifications.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Identity.Notifications.Extensions;

/// <summary>
/// Extension methods for registering the identity-backed recipient resolver.
/// </summary>
public static class IdentityNotificationsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <c>IdentityRecipientResolver</c> as the
    /// <see cref="IRecipientResolver"/> implementation, resolving recipients
    /// from the Identity module's <see cref="IIdentityUserReader"/>.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="ServiceCollectionDescriptorExtensions.TryAddScoped{TService, TImplementation}(IServiceCollection)"/>
    /// so an application-supplied <see cref="IRecipientResolver"/> registered
    /// earlier takes precedence.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitIdentityRecipientResolver(this IServiceCollection services)
    {
        services.AddOptions<IdentityRecipientResolverOptions>()
            .BindConfiguration(IdentityRecipientResolverOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddScoped<IRecipientResolver, IdentityRecipientResolver>();

        return services;
    }
}
