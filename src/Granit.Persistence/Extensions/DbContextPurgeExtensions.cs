using System.Reflection;
using Granit.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Granit.Persistence.Extensions;

/// <summary>
/// Extension methods for purging soft-deleted records past the ISO 27001 retention period.
/// </summary>
/// <remarks>
/// These methods perform <b>hard deletes</b> and bypass EF Core change tracking and interceptors.
/// They are intended exclusively for post-retention archival (records older than 3 years per ISO 27001).
/// Provider-agnostic: uses EF Core's <c>ExecuteDeleteAsync</c> (works with PostgreSQL, SQL Server, SQLite).
/// </remarks>
public static class DbContextPurgeExtensions
{
    // S3011: intentional — generic EF Core bulk-delete pattern requires reflection on private generic method
    private static readonly MethodInfo PurgeEntityMethod =
        typeof(DbContextPurgeExtensions)
            .GetMethod(nameof(PurgeEntityAsync), BindingFlags.Static | BindingFlags.NonPublic)!;

    /// <summary>
    /// Hard-deletes all <see cref="ISoftDeletable"/> records where <c>DeletedAt</c> is before
    /// <paramref name="cutoff"/>, across all entity types in the model that implement
    /// <see cref="ISoftDeletable"/>.
    /// </summary>
    /// <param name="context">The DbContext to purge records from.</param>
    /// <param name="cutoff">All soft-deleted records with <c>DeletedAt</c> strictly before this value are removed.</param>
    /// <param name="batchSize">Maximum number of records to delete per entity type.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total number of records deleted across all entity types.</returns>
    public static async Task<int> PurgeSoftDeletedBeforeAsync(
        this DbContext context,
        DateTimeOffset cutoff,
        int batchSize = 1000,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        int totalDeleted = 0;

        foreach (Type? clrType in context.Model.GetEntityTypes()
            .Where(et => typeof(ISoftDeletable).IsAssignableFrom(et.ClrType))
            .Select(et => et.ClrType))
        {
            MethodInfo generic = PurgeEntityMethod.MakeGenericMethod(clrType);
            int deleted = await ((Task<int>)generic.Invoke(null, [context, cutoff, batchSize, cancellationToken])!)
                .ConfigureAwait(false);
            totalDeleted += deleted;
        }

        return totalDeleted;
    }

    // NOSONAR S3011 - intentional: generic EF Core bulk-delete pattern requires reflection on private generic method
    private static async Task<int> PurgeEntityAsync<TEntity>(
        DbContext context,
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken)
        where TEntity : class, ISoftDeletable =>
        await context.Set<TEntity>()
            .IgnoreQueryFilters([GranitFilterNames.SoftDelete])
            .Where(e => e.IsDeleted && e.DeletedAt != null && e.DeletedAt < cutoff)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
}
