using System.Collections.ObjectModel;
using System.Reflection;
using Granit.DataExchange.Export;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Export;

/// <summary>
/// Discovers entity types eligible for auto-generated export definitions
/// by scanning all registered EF Core <see cref="DbContext"/> models.
/// </summary>
/// <remarks>
/// <para>
/// Entity types are discovered lazily on first access by resolving each
/// <see cref="DbContext"/> registered in DI and reading its <see cref="IModel"/>.
/// Results are cached for the lifetime of the singleton (thread-safe via <see cref="Lazy{T}"/>).
/// </para>
/// <para>
/// Owned types, keyless types, and shadow CLR types are excluded —
/// only top-level mapped entities with a CLR type are returned.
/// </para>
/// </remarks>
internal sealed partial class DbContextAutoExportDefinitionSource(
    IServiceProvider serviceProvider,
    ILogger<DbContextAutoExportDefinitionSource> logger) : IAutoExportDefinitionSource
{
    private readonly Lazy<IReadOnlyList<Type>> _entityTypes = new(
        () => DiscoverEntityTypes(serviceProvider, logger),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <inheritdoc/>
    public IReadOnlyList<Type> GetEntityTypes() => _entityTypes.Value;

    private static ReadOnlyCollection<Type> DiscoverEntityTypes(
        IServiceProvider sp,
        ILogger logger)
    {
        HashSet<Type> entityTypes = [];
        ReadOnlyCollection<Type> dbContextTypes = FindDbContextTypes(sp);

        using IServiceScope scope = sp.CreateScope();

        foreach (Type dbContextType in dbContextTypes)
        {
            try
            {
                if (scope.ServiceProvider.GetService(dbContextType) is not DbContext context)
                {
                    continue;
                }

                using (context)
                {
                    foreach (IEntityType entityType in context.Model.GetEntityTypes())
                    {
                        // Skip owned types, shadow types, and keyless types
                        if (entityType.IsOwned())
                        {
                            continue;
                        }

                        if (entityType.FindPrimaryKey() is null)
                        {
                            continue;
                        }

                        Type clrType = entityType.ClrType;
                        if (clrType.IsAbstract)
                        {
                            continue;
                        }

                        entityTypes.Add(clrType);
                    }
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                LogDbContextScanFailed(logger, dbContextType.Name, ex);
            }
        }

        LogDiscoveredEntityTypes(logger, entityTypes.Count, dbContextTypes.Count);
        return entityTypes.ToList().AsReadOnly();
    }

    private static ReadOnlyCollection<Type> FindDbContextTypes(IServiceProvider sp)
    {
        // Scan service descriptors for DbContext registrations.
        // DbContexts are registered directly (via AddDbContextFactory)
        // and resolved as their concrete type.
        HashSet<Type> contextTypes = [];

        IServiceCollection? services = sp.GetService<IServiceCollection>();
        if (services is not null)
        {
            foreach (ServiceDescriptor descriptor in services)
            {
                if (descriptor.ServiceType.IsGenericType &&
                    descriptor.ServiceType.GetGenericTypeDefinition() == typeof(IDbContextFactory<>))
                {
                    Type dbContextType = descriptor.ServiceType.GetGenericArguments()[0];
                    contextTypes.Add(dbContextType);
                }
            }
        }

        // Fallback: scan loaded assemblies for DbContext subclasses if no service collection
        if (contextTypes.Count == 0)
        {
            foreach (Type type in AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a.GetName().Name?.StartsWith("Granit", StringComparison.Ordinal) == true)
                .SelectMany(a =>
                {
                    try { return a.GetTypes(); }
                    catch (ReflectionTypeLoadException ex)
                    {
                        return ex.Types.Where(t => t is not null).Cast<Type>();
                    }
                }))
            {
                if (!type.IsAbstract && type.IsSubclassOf(typeof(DbContext)))
                {
                    contextTypes.Add(type);
                }
            }
        }

        return contextTypes.ToList().AsReadOnly();
    }

    [LoggerMessage(1, LogLevel.Warning, "Failed to scan DbContext '{DbContextName}' for entity types")]
    private static partial void LogDbContextScanFailed(ILogger logger, string dbContextName, Exception ex);

    [LoggerMessage(2, LogLevel.Information, "Auto-export: discovered {EntityCount} entity types from {DbContextCount} DbContexts")]
    private static partial void LogDiscoveredEntityTypes(ILogger logger, int entityCount, int dbContextCount);
}
