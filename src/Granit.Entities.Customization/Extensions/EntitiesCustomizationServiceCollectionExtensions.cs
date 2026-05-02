using Granit.Entities.Customization.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Entities.Customization.Extensions;

/// <summary>
/// DI extensions for the <c>Granit.Entities.Customization</c> runtime module.
/// </summary>
public static class EntitiesCustomizationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default no-op <see cref="IEntityCustomizationReader"/>
    /// (<see cref="NullEntityCustomizationReader"/>). The EF companion (B2)
    /// replaces this binding via <c>services.Replace(...)</c>; hosts that
    /// stop at this layer still boot — every customization lookup returns
    /// empty so the manifest composer falls through to compiled defaults.
    /// </summary>
    public static IServiceCollection AddGranitEntitiesCustomization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddScoped<IEntityCustomizationReader, NullEntityCustomizationReader>();
        return services;
    }
}
