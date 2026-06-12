using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Granit.Indexing.EntityFrameworkCore.Internal;

/// <summary>
/// EF-backed <see cref="IIndexedDataEraser"/>: deletes every
/// <see cref="IndexedEntryRow{TKey}"/> tied to <c>(TenantId, DataSubjectId)</c> across
/// every registered <c>TKey</c>, in a single <c>ExecuteDelete</c> per table.
/// </summary>
/// <remarks>
/// Bypasses the multi-tenant query filter (<c>IgnoreQueryFilters</c>) so the call site
/// can erase across tenants when invoked from the privacy bridge with an explicit
/// <c>TenantId</c> from the deletion event.
/// </remarks>
internal sealed class EfIndexedDataEraser : IIndexedDataEraser
{
    private readonly IDbContextFactory<IndexingDbContext> _factory;

    public EfIndexedDataEraser(IDbContextFactory<IndexingDbContext> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    public string Name => EfIndexer<Guid>.BackendName;

    public async Task<int> EraseAsync(Guid? tenantId, Guid dataSubjectId, CancellationToken cancellationToken = default)
    {
        await using IndexingDbContext db = await _factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        int total = 0;
        foreach (Type keyType in db.IndexedKeyTypes)
        {
            var delete = (Task<int>)DeleteByDataSubjectMethod
                .MakeGenericMethod(keyType)
                .Invoke(null, [db, tenantId, dataSubjectId, cancellationToken])!;
            total += await delete.ConfigureAwait(false);
        }

        return total;
    }

    [SuppressMessage("Major Code Smell", "S3011:Reflection should not be used to increase accessibility of classes, methods, or fields",
        Justification = "Reflects over this type's OWN private DeleteByDataSubjectAsync<TKey> to close it over a key " +
            "type known only at runtime (db.IndexedKeyTypes). It does not bypass another type's encapsulation; the " +
            "method is private precisely to stay out of the public API while remaining reflectively dispatchable.")]
    private static readonly MethodInfo DeleteByDataSubjectMethod =
        typeof(EfIndexedDataEraser).GetMethod(
            nameof(DeleteByDataSubjectAsync),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    private static Task<int> DeleteByDataSubjectAsync<TKey>(
        IndexingDbContext db, Guid? tenantId, Guid dataSubjectId, CancellationToken cancellationToken) =>
        db.Set<IndexedEntryRow<TKey>>()
            .IgnoreQueryFilters([Granit.Persistence.EntityFrameworkCore.GranitFilterNames.MultiTenant])
            .Where(r => r.TenantId == tenantId && r.DataSubjectId == dataSubjectId)
            .ExecuteDeleteAsync(cancellationToken);
}
