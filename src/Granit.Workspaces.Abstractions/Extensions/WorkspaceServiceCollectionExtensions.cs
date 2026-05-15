using Microsoft.Extensions.DependencyInjection;

namespace Granit.Workspaces.Extensions;

/// <summary>
/// Service-collection extensions for registering workspace definitions and
/// feature providers.
/// </summary>
public static class WorkspaceServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TWorkspaceDefinition"/> as a singleton, plus
    /// the matching <see cref="IWorkspaceDescriptor"/> alias so the runtime can
    /// enumerate every registered definition in one shot.
    /// </summary>
    public static IServiceCollection AddWorkspaceDefinition<TWorkspaceDefinition>(this IServiceCollection services)
        where TWorkspaceDefinition : WorkspaceDefinition
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<TWorkspaceDefinition>();
        services.AddSingleton<WorkspaceDefinition>(sp => sp.GetRequiredService<TWorkspaceDefinition>());
        services.AddSingleton<IWorkspaceDescriptor>(sp => sp.GetRequiredService<TWorkspaceDefinition>());
        return services;
    }

    /// <summary>
    /// Registers <typeparamref name="TProvider"/> as a singleton implementing
    /// <see cref="IFeatureProvider"/> (per ADR-057). Every registered provider
    /// is invoked once when the <see cref="IFeatureCatalog"/> is first
    /// resolved; the result is frozen for the lifetime of the host.
    /// </summary>
    public static IServiceCollection AddFeatureProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IFeatureProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IFeatureProvider, TProvider>();
        return services;
    }
}
