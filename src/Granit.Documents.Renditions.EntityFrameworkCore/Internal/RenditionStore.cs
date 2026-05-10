using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.Renditions.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Documents.Renditions.EntityFrameworkCore.Internal;

/// <summary>EF Core-backed <see cref="IRenditionStore"/>.</summary>
internal sealed class RenditionStore(IDbContextFactory<RenditionsDbContext> contextFactory) : IRenditionStore
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentRendition>> ListForVersionAsync(
        Guid documentVersionId, CancellationToken cancellationToken = default)
    {
        await using RenditionsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await context.Renditions
            .Where(r => r.DocumentVersionId == documentVersionId)
            .OrderBy(r => r.Type)
            .ThenBy(r => r.Format)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentRendition>> ListForDocumentAsync(
        Guid documentId, CancellationToken cancellationToken = default)
    {
        await using RenditionsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Bypass tenant filter — cascade handlers can run cross-tenant.
        return await context.Renditions
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .Where(r => r.DocumentId == documentId)
            .OrderBy(r => r.Type)
            .ThenBy(r => r.Format)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<DocumentRendition?> FindAsync(
        Guid documentVersionId,
        RenditionType type,
        string format,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        await using RenditionsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await context.Renditions
            .FirstOrDefaultAsync(
                r => r.DocumentVersionId == documentVersionId && r.Type == type && r.Format == format,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddAsync(DocumentRendition rendition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rendition);

        await using RenditionsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        context.Renditions.Add(rendition);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(DocumentRendition rendition, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rendition);

        await using RenditionsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        context.Renditions.Update(rendition);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteForDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await using RenditionsDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Bypass the tenant filter — the cascade runs from the document permanent-delete
        // handler which is not necessarily scoped to a tenant context (background jobs
        // may issue the delete cross-tenant).
        await context.Renditions
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .Where(r => r.DocumentId == documentId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
