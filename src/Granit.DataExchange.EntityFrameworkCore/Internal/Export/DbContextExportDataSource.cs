using System.Reflection;
using Granit.DataExchange.Export;
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
/// The queryable is returned with <c>AsNoTracking()</c> for export performance —
/// no change tracking is needed for read-only export.
/// </para>
/// <para>
/// Navigation properties are NOT auto-included. The reflection-based fallback
/// only exposes simple (non-navigation) properties, so missing <c>Include()</c>
/// calls are not a problem. For navigation fields, use an explicit
/// <see cref="ExportDefinition{TEntity}"/> + <see cref="IExportDataSource{TEntity}"/>.
/// </para>
/// </remarks>
internal sealed class DbContextExportDataSource<TEntity>(
    IServiceProvider serviceProvider) : IExportDataSource<TEntity>
    where TEntity : class
{
    /// <inheritdoc/>
    public IQueryable<TEntity> GetQueryable()
    {
        DbContext context = ResolveDbContext();
        return context.Set<TEntity>().AsNoTracking();
    }

    private DbContext ResolveDbContext()
    {
        // Scan loaded Granit assemblies for DbContext subclasses
        IEnumerable<Type> dbContextTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name?.StartsWith("Granit", StringComparison.Ordinal) == true)
            .SelectMany(a =>
            {
                try { return a.GetTypes(); }
                catch (ReflectionTypeLoadException ex)
                {
                    return ex.Types.Where(t => t is not null).Cast<Type>();
                }
            })
            .Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(DbContext)));

        foreach (Type dbContextType in dbContextTypes)
        {
            if (serviceProvider.GetService(dbContextType) is not DbContext context)
            {
                continue;
            }

            if (context.Model.FindEntityType(typeof(TEntity)) is not null)
            {
                return context;
            }

            context.Dispose();
        }

        throw new InvalidOperationException(
            $"No registered DbContext contains entity type '{typeof(TEntity).Name}'. " +
            $"Register an explicit IExportDataSource<{typeof(TEntity).Name}> or ensure " +
            $"the entity is mapped in a DbContext.");
    }
}
