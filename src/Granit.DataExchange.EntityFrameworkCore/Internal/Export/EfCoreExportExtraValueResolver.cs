using System.Collections.Concurrent;
using Granit.DataExchange.Export;
using Granit.Persistence.EntityFrameworkCore.ExtraProperties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Export;

/// <summary>
/// <see cref="IExportExtraValueResolver"/> that reads shadow property values via
/// <c>DbContext.Entry(entity).Property(name).CurrentValue</c>.
/// </summary>
/// <remarks>
/// Requires entities to be tracked (no <c>AsNoTracking()</c>). The
/// <see cref="DbContextExportDataSource{TEntity}"/> disables no-tracking
/// when mapped extra properties are detected.
/// </remarks>
internal sealed class EfCoreExportExtraValueResolver(
    IServiceProvider serviceProvider,
    IExtraPropertyMappingRegistry registry) : IExportExtraValueResolver
{
    private readonly ConcurrentDictionary<Type, DbContext?> _contextCache = new();

    public object? ResolveExtraValue(object entity, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(entity);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        Type entityType = entity.GetType();
        if (!registry.GetMappedPropertyNames(entityType).Contains(propertyName))
        {
            return null;
        }

        DbContext? context = _contextCache.GetOrAdd(
            entityType, type => DbContextResolver.TryResolve(serviceProvider, type));

        if (context is null)
        {
            return null;
        }

        EntityEntry entry = context.Entry(entity);
        PropertyEntry? property = entry.Properties.FirstOrDefault(p =>
            string.Equals(p.Metadata.Name, propertyName, StringComparison.Ordinal));

        return property?.CurrentValue;
    }
}
