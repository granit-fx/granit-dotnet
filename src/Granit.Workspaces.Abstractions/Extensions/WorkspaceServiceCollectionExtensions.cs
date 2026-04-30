using Microsoft.Extensions.DependencyInjection;

namespace Granit.Workspaces.Extensions;

/// <summary>
/// Service-collection extensions for registering workspace definitions and
/// cross-module contributions.
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
    /// Registers <typeparamref name="TContributor"/> as a singleton implementing
    /// <see cref="IWorkspaceContributor"/>. Multiple contributors per host are
    /// expected — invocation order at boot is registration order.
    /// </summary>
    public static IServiceCollection AddWorkspaceContribution<TContributor>(this IServiceCollection services)
        where TContributor : class, IWorkspaceContributor
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IWorkspaceContributor, TContributor>();
        return services;
    }
}
