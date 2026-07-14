using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Export;

/// <summary>
/// Shared utility for discovering the <see cref="DbContext"/> that maps a given entity type.
/// Enumerates contexts declared via <c>AddDataExchangeDbContext&lt;TContext&gt;()</c>.
/// </summary>
/// <remarks>
/// Resolved context instances are owned by the DI scope — they are never disposed here.
/// Disposing them would break any later consumer of the same scoped context in the request.
/// </remarks>
internal static class DbContextResolver
{
    /// <summary>
    /// Resolves the <see cref="DbContext"/> containing the specified entity type.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve DbContext instances from.</param>
    /// <param name="entityType">The CLR entity type to look up.</param>
    /// <returns>The resolved DbContext, or <see langword="null"/> if no registered context maps the type.</returns>
    internal static DbContext? TryResolve(IServiceProvider serviceProvider, Type entityType)
    {
        foreach (DataExchangeDbContextRegistration registration
                 in serviceProvider.GetServices<DataExchangeDbContextRegistration>())
        {
            if (serviceProvider.GetService(registration.ContextType) is not DbContext context)
            {
                continue;
            }

            if (context.Model.FindEntityType(entityType) is not null)
            {
                return context;
            }
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
                   $"Register an explicit IExportDataSource<{entityType.Name}>, or declare the owning " +
                   "context via services.AddDataExchangeDbContext<TContext>().");
    }
}
