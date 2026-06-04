using Granit.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Export;

/// <summary>
/// Shared utility for discovering the <see cref="DbContext"/> that maps a given entity type.
/// Scans loaded Granit assemblies for <see cref="DbContext"/> subclasses registered in DI.
/// </summary>
internal static class DbContextResolver
{
    /// <summary>
    /// Resolves the <see cref="DbContext"/> containing the specified entity type.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve DbContext instances from.</param>
    /// <param name="entityType">The CLR entity type to look up.</param>
    /// <returns>The resolved DbContext, or <see langword="null"/> if no matching context is found.</returns>
    internal static DbContext? TryResolve(IServiceProvider serviceProvider, Type entityType)
    {
        IEnumerable<Type> dbContextTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Granit", StringComparison.Ordinal) == true)
            .SelectMany(SafeTypeLoader.GetLoadableTypes)
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(DbContext)));

        foreach (Type dbContextType in dbContextTypes)
        {
            if (serviceProvider.GetService(dbContextType) is not DbContext context)
            {
                continue;
            }

            if (context.Model.FindEntityType(entityType) is not null)
            {
                return context;
            }

            context.Dispose();
        }

        return null;
    }

    /// <summary>
    /// Resolves the <see cref="DbContext"/> containing the specified entity type.
    /// Throws if no matching context is found.
    /// </summary>
    /// <exception cref="InvalidOperationException">No registered DbContext maps the entity type.</exception>
    internal static DbContext Resolve(IServiceProvider serviceProvider, Type entityType)
    {
        return TryResolve(serviceProvider, entityType)
               ?? throw new InvalidOperationException(
                   $"No registered DbContext contains entity type '{entityType.Name}'. " +
                   $"Register an explicit IExportDataSource<{entityType.Name}> or ensure " +
                   $"the entity is mapped in a DbContext.");
    }
}
