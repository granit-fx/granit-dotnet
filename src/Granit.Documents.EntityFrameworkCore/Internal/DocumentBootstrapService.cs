using System.Threading;
using Granit.Documents.Domain;
using Granit.Guids;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Documents.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core-backed implementation of <see cref="IDocumentBootstrapService"/>.
/// Lazy, idempotent, concurrency-safe.
/// </summary>
internal sealed class DocumentBootstrapService(
    IDbContextFactory<DocumentsDbContext> contextFactory,
    IGuidGenerator guidGenerator) : IDocumentBootstrapService, IDisposable
{
    /// <summary>Sentinel mapping <c>tenantId == null</c> (host scope) to a non-nullable cache key.</summary>
    private static readonly Guid HostSentinel = Guid.Empty;

    /// <summary>Per-instance cache of resolved tenant-root identifiers (scope-lifetime memoisation).</summary>
    private readonly Dictionary<Guid, Guid> _resolved = [];

    /// <summary>Serialises in-process bootstrap calls so the SELECT/INSERT pair is atomic against the cache.</summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <inheritdoc/>
    public async Task<Guid> EnsureTenantRootAsync(
        Guid? tenantId,
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        Guid cacheKey = tenantId ?? HostSentinel;
        if (_resolved.TryGetValue(cacheKey, out Guid cached))
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_resolved.TryGetValue(cacheKey, out cached))
            {
                return cached;
            }

            await using DocumentsDbContext context = await contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);

            Guid? existing = await SelectRootIdAsync(context, tenantId, cancellationToken).ConfigureAwait(false);
            if (existing.HasValue)
            {
                _resolved[cacheKey] = existing.Value;
                return existing.Value;
            }

            var root = Folder.CreateTenantRoot(guidGenerator.Create(), tenantId, ownerUserId);
            context.Folders.Add(root);

            try
            {
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _resolved[cacheKey] = root.Id;
                return root.Id;
            }
            catch (DbUpdateException)
            {
                // A concurrent bootstrap inserted the root between our SELECT and INSERT.
                // The partial unique index ux_documents_folders_one_root_per_tenant rejected
                // our row; re-issue the SELECT to obtain the winning identifier.
                Guid? winner = await SelectRootIdAsync(context, tenantId, cancellationToken)
                    .ConfigureAwait(false);
                if (winner.HasValue)
                {
                    _resolved[cacheKey] = winner.Value;
                    return winner.Value;
                }
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _gate.Dispose();

    private static async Task<Guid?> SelectRootIdAsync(
        DocumentsDbContext context,
        Guid? tenantId,
        CancellationToken ct)
    {
        // Bypass the multi-tenant filter only — the bootstrap may run for the host scope
        // (tenantId == null) while ICurrentTenant is set, in which case the multi-tenant
        // query filter would otherwise exclude the row we are looking for. Other filters
        // (soft-delete, etc.) stay active. The explicit `f.TenantId == tenantId` predicate
        // makes the result independent of the ambient ICurrentTenant.
        return await context.Folders
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .Where(f => f.TenantId == tenantId && f.IsTenantRoot)
            .Select(f => (Guid?)f.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }
}
