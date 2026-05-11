using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Granit.Documents.AssetMetadata.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Documents.AssetMetadata.EntityFrameworkCore.Internal;

/// <summary>EF Core-backed <see cref="IAssetMetadataStore"/>.</summary>
internal sealed class AssetMetadataStore(IDbContextFactory<AssetMetadataDbContext> contextFactory) : IAssetMetadataStore
{
    /// <inheritdoc />
    public async Task AddAsync(DocumentAssetMetadata metadata, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        await using AssetMetadataDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        context.AssetMetadata.Add(metadata);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(DocumentAssetMetadata metadata, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        await using AssetMetadataDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        context.AssetMetadata.Update(metadata);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<DocumentAssetMetadata?> GetByVersionAsync(
        Guid documentVersionId, CancellationToken cancellationToken = default)
    {
        await using AssetMetadataDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await context.AssetMetadata
            .FirstOrDefaultAsync(m => m.DocumentVersionId == documentVersionId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentAssetMetadata>> ListForDocumentAsync(
        Guid documentId, CancellationToken cancellationToken = default)
    {
        await using AssetMetadataDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Bypass tenant filter — cascade handlers can run cross-tenant.
        return await context.AssetMetadata
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .Where(m => m.DocumentId == documentId)
            // Order by Id — Granit ships sequential GUIDs so this is effectively
            // chronological; SQLite cannot ORDER BY a DateTimeOffset column.
            .OrderBy(m => m.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DeleteForDocumentAsync(Guid documentId, CancellationToken cancellationToken = default)
    {
        await using AssetMetadataDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        await context.AssetMetadata
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .Where(m => m.DocumentId == documentId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
