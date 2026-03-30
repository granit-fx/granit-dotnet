using Granit.Privacy.Regulations.Internal;
using Granit.Privacy.Regulations.Options;
using Granit.Privacy.Regulations.Profiles;
using Granit.Privacy.Regulations.Profiles.Internal;
using Granit.Privacy.Regulations.ResponseDeadline;
using Granit.Privacy.Regulations.ResponseDeadline.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Privacy.Regulations.Extensions;

/// <summary>
/// Extension methods for registering <c>Granit.Privacy.Regulations</c> services.
/// </summary>
public static class PrivacyRegulationsServiceCollectionExtensions
{
    /// <summary>
    /// Registers the privacy regulations module: regulation profile registry, resolver, and built-in providers.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration (for <c>Privacy:Regulations</c> section).</param>
    /// <param name="configure">Optional callback to customize registration (e.g., add Tier 3 providers).</param>
    public static IServiceCollection AddGranitPrivacyRegulations(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<GranitPrivacyRegulationsBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // Bind options
        services.Configure<PrivacyRegulationsOptions>(
            configuration.GetSection(PrivacyRegulationsOptions.SectionName));

        // Allow external customization
        var builder = new GranitPrivacyRegulationsBuilder(services);
        configure?.Invoke(builder);

        // Build the registry from all providers
        var context = new RegulationProfileContext();

        // Built-in Tier 1 + Tier 2
        new BuiltInRegulationProfileProvider().Define(context);

        // Custom Tier 3 providers
        foreach (IRegulationProfileProvider provider in builder.Providers)
        {
            provider.Define(context);
        }

        var registry = new RegulationProfileRegistry(context.Build());
        services.TryAddSingleton<IRegulationProfileRegistry>(registry);

        // Resolver (scoped — depends on ICurrentTenant)
        services.TryAddScoped<IPrivacyRegulationResolver, TenantBasedRegulationResolver>();

        // Deadline tracker
        services.TryAddSingleton<IResponseDeadlineTracker, DefaultResponseDeadlineTracker>();

        return services;
    }
}
