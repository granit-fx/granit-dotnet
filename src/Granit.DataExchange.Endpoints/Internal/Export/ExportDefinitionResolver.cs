using Granit.DataExchange.Export;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.DataExchange.Endpoints.Internal.Export;

/// <summary>
/// Runtime resolution helper for export definitions.
/// Uses <see cref="IExportDefinitionProvider"/> when available (explicit + auto-generated),
/// falls back to raw DI enumeration for backward compatibility.
/// </summary>
internal static class ExportDefinitionResolver
{
    /// <summary>
    /// Finds an <see cref="IExportDefinitionDescriptor"/> by name from the registered definitions.
    /// </summary>
    internal static IExportDefinitionDescriptor? FindByName(
        IServiceProvider serviceProvider,
        string definitionName)
    {
        IExportDefinitionProvider? provider = serviceProvider.GetService<IExportDefinitionProvider>();
        if (provider is not null)
        {
            return provider.FindByName(definitionName);
        }

        // Backward compat: no provider registered
        IEnumerable<IExportDefinitionDescriptor> descriptors =
            serviceProvider.GetServices<IExportDefinitionDescriptor>();
        return descriptors.FirstOrDefault(d =>
            string.Equals(d.Name, definitionName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns all available export definitions (explicit + auto-generated).
    /// </summary>
    internal static IEnumerable<IExportDefinitionDescriptor> GetAll(
        IServiceProvider serviceProvider)
    {
        IExportDefinitionProvider? provider = serviceProvider.GetService<IExportDefinitionProvider>();
        if (provider is not null)
        {
            return provider.GetAll();
        }

        return serviceProvider.GetServices<IExportDefinitionDescriptor>();
    }
}
