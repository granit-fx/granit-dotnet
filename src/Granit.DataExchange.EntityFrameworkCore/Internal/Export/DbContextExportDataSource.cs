using Granit.DataExchange.Export;
using Granit.Domain;
using Granit.Persistence.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Export;

/// <summary>
/// Open-generic fallback <see cref="IExportDataSource{TEntity}"/> that discovers
/// the <see cref="DbContext"/> containing <typeparamref name="TEntity"/> at runtime.
/// </summary>
/// <typeparam name="TEntity">The entity type to export.</typeparam>
/// <remarks>
/// <para>
/// Registered via <c>TryAddScoped(typeof(IExportDataSource&lt;&gt;), typeof(DbContextExportDataSource&lt;&gt;))</c>,
/// so that explicit per-entity <c>IExportDataSource&lt;T&gt;</c> registrations always take precedence.
/// </para>
/// <para>
/// When the entity implements <see cref="IHasMetadata"/> and has mapped extra
/// properties in the <see cref="IMetadataMappingRegistry"/>, tracking is enabled
/// so shadow property values can be read via <c>DbContext.Entry()</c>. The caller
/// (orchestrator) is responsible for periodic <see cref="Microsoft.EntityFrameworkCore.ChangeTracking.ChangeTracker"/>
/// clearing to prevent memory bloat.
/// </para>
/// <para>
/// Navigation properties are NOT auto-included. The reflection-based fallback
/// only exposes simple (non-navigation) properties, so missing <c>Include()</c>
/// calls are not a problem. For navigation fields, use an explicit
/// <see cref="ExportDefinition{TEntity}"/> + <see cref="IExportDataSource{TEntity}"/>.
/// </para>
/// </remarks>
internal sealed class DbContextExportDataSource<TEntity>(
    IServiceProvider serviceProvider,
    IMetadataMappingRegistry registry) : IExportDataSource<TEntity>
    where TEntity : class
{
    /// <inheritdoc/>
    public IQueryable<TEntity> GetQueryable()
    {
        DbContext context = DbContextResolver.Resolve(serviceProvider, typeof(TEntity));

        bool needsTracking = typeof(IHasMetadata).IsAssignableFrom(typeof(TEntity))
                             && registry.GetMappedPropertyNames(typeof(TEntity)).Count > 0;

        return needsTracking
            ? context.Set<TEntity>()
            : context.Set<TEntity>().AsNoTracking();
    }
}
