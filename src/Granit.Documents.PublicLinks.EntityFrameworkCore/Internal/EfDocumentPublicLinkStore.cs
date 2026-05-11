using Granit.Documents.PublicLinks.Domain;
using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Granit.Documents.PublicLinks.EntityFrameworkCore.Internal;

/// <summary>EF Core-backed <see cref="IDocumentPublicLinkStore"/>.</summary>
internal sealed class EfDocumentPublicLinkStore(IDbContextFactory<DocumentsPublicLinksDbContext> contextFactory)
    : IDocumentPublicLinkStore
{
    /// <inheritdoc/>
    public async Task<DocumentPublicLink?> ResolveByTokenHashAsync(
        byte[] tokenHash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tokenHash);

        await using DocumentsPublicLinksDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Anonymous redemption: no tenant context; the link carries its own.
        return await context.DocumentPublicLinks
            .IgnoreQueryFilters([GranitFilterNames.MultiTenant])
            .FirstOrDefaultAsync(l => l.TokenHash == tokenHash, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DocumentPublicLink>> ListForDocumentAsync(
        Guid documentId, CancellationToken cancellationToken = default)
    {
        await using DocumentsPublicLinksDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        // Order by Id desc — Granit ships sequential GUIDs so this is effectively
        // newest-first; SQLite cannot ORDER BY a DateTimeOffset column.
        return await context.DocumentPublicLinks
            .Where(l => l.DocumentId == documentId)
            .OrderByDescending(l => l.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<DocumentPublicLink?> FindByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        await using DocumentsPublicLinksDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        return await context.DocumentPublicLinks
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task AddAsync(DocumentPublicLink link, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(link);

        await using DocumentsPublicLinksDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        context.DocumentPublicLinks.Add(link);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(DocumentPublicLink link, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(link);

        await using DocumentsPublicLinksDbContext context = await contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        context.DocumentPublicLinks.Update(link);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
