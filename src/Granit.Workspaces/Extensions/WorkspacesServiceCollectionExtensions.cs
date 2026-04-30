using Granit.Workspaces.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Workspaces.Extensions;

/// <summary>
/// Service-collection extensions for the <c>Granit.Workspaces</c> runtime.
/// </summary>
public static class WorkspacesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="IWorkspaceRegistry"/> singleton. The registry
    /// is built lazily on first resolve from every registered
    /// <see cref="IWorkspaceDescriptor"/> + <see cref="IWorkspaceContributor"/>.
    /// </summary>
    public static IServiceCollection AddGranitWorkspaces(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IWorkspaceRegistry, WorkspaceRegistry>();
        return services;
    }
}
