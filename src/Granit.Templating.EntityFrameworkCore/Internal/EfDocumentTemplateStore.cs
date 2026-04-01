using Granit.Domain;
using Granit.Exceptions;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Templating.EntityFrameworkCore.Entities;
using Granit.Templating.Exceptions;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
using Granit.Templating.Store;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Granit.Templating.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IDocumentTemplateStoreReader"/> and <see cref="IDocumentTemplateStoreWriter"/>.
/// Manages the Draft → Published → Archived lifecycle of template revisions.
/// </summary>
/// <remarks>
/// Each operation creates and disposes its own <see cref="TemplatingDbContext"/>
/// via <see cref="IDbContextFactory{TContext}"/>, making concurrent access safe.
/// <para>
/// <strong>HybridCache:</strong> <c>TryGetPublishedAsync</c> caches results in L1 (in-memory)
/// and optional L2 (distributed). Cache entries are invalidated on <c>PublishAsync</c>
/// and <c>UnpublishAsync</c> to prevent stale reads after lifecycle transitions.
/// </para>
/// <para>
/// <strong>ISO 27001 compliance:</strong> only <c>Draft</c> revisions are physically deleted.
/// <c>Published</c> → <c>Archived</c> transitions are always preserved for the 3-year audit trail.
/// </para>
/// </remarks>
internal sealed class EfDocumentTemplateStore(
    IDbContextFactory<TemplatingDbContext> contextFactory,
    HybridCache cache,
    IGuidGenerator guidGenerator,
    IClock clock,
    ITemplateTransitionHook transitionHook,
    ICurrentTenant? currentTenant = null) : IDocumentTemplateStoreReader, IDocumentTemplateStoreWriter
{
    /// <inheritdoc/>
    public async Task<TemplateDescriptor?> TryGetPublishedAsync(
        TemplateKey key, CancellationToken cancellationToken = default)
    {
        TemplateCacheEntry entry = await cache.GetOrCreateAsync(
            CacheKey(key),
            async innerCt =>
            {
                await using TemplatingDbContext ctx = await contextFactory.CreateDbContextAsync(innerCt).ConfigureAwait(false);
                TemplateRevisionEntity? entity = await ctx.TemplateRevisions
                    .Where(r => r.TemplateName == key.Name
                                && r.Culture == key.Culture
                                && r.Status == TemplateLifecycleStatus.Published)
                    .FirstOrDefaultAsync(innerCt).ConfigureAwait(false);

                return entity is null
                    ? TemplateCacheEntry.NotFound
                    : TemplateCacheEntry.From(entity.Content, entity.MimeType, entity.RevisionId, entity.LayoutName);
            },
            cancellationToken: cancellationToken);

        return entry.ToDescriptor();
    }

    /// <inheritdoc/>
    public async Task SaveDraftAsync(
        TemplateKey key,
        string content,
        string mimeType,
        string updatedBy,
        string? layoutName = null,
        CancellationToken cancellationToken = default)
    {
        await using TemplatingDbContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TemplateRevisionEntity? existing = await ctx.TemplateRevisions
            .Where(r => r.TemplateName == key.Name
                        && r.Culture == key.Culture
                        && r.Status == TemplateLifecycleStatus.Draft)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            // Update existing draft in place (only one draft per key)
            existing.Content = content;
            existing.MimeType = mimeType;
            existing.CreatedBy = updatedBy;
            existing.CreatedAt = clock.Now;
            existing.LayoutName = layoutName;
        }
        else
        {
            ctx.TemplateRevisions.Add(new TemplateRevisionEntity
            {
                RevisionId = guidGenerator.Create(),
                TemplateName = key.Name,
                Culture = key.Culture,
                Content = content,
                MimeType = mimeType,
                Status = TemplateLifecycleStatus.Draft,
                CreatedAt = clock.Now,
                CreatedBy = updatedBy,
                LayoutName = layoutName,
                TenantId = currentTenant is { IsAvailable: true } ? currentTenant.Id : null,
            });
        }

        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task PublishAsync(
        TemplateKey key,
        string publishedBy,
        CancellationToken cancellationToken = default)
    {
        if (!await transitionHook.CanTransitionAsync(TemplateLifecycleStatus.Draft, TemplateLifecycleStatus.Published, cancellationToken).ConfigureAwait(false))
        {
            throw new TemplateTransitionDeniedException(TemplateLifecycleStatus.Draft, TemplateLifecycleStatus.Published);
        }

        await using TemplatingDbContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TemplateRevisionEntity? draft = await ctx.TemplateRevisions
            .Where(r => r.TemplateName == key.Name
                        && r.Culture == key.Culture
                        && r.Status == TemplateLifecycleStatus.Draft)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (draft is null)
        {
            throw new NotFoundException(
                $"Cannot publish template '{key.Name}' (culture: {key.Culture ?? "neutral"}): no draft exists.");
        }

        // Archive any currently published revision (ISO 27001: row is kept, status changes)
        List<TemplateRevisionEntity> currentlyPublished = await ctx.TemplateRevisions
            .Where(r => r.TemplateName == key.Name
                        && r.Culture == key.Culture
                        && r.Status == TemplateLifecycleStatus.Published)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        DateTimeOffset now = clock.Now;
        foreach (TemplateRevisionEntity published in currentlyPublished)
        {
            published.Status = TemplateLifecycleStatus.Archived;
            published.ArchivedAt = now;
            published.ArchivedBy = publishedBy;
        }

        draft.Status = TemplateLifecycleStatus.Published;
        draft.PublishedAt = now;
        draft.PublishedBy = publishedBy;

        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Notify hook after persistence (archival of previous + promotion of draft)
        foreach (TemplateRevisionEntity archived in currentlyPublished)
        {
            await transitionHook.OnTransitionedAsync(
                archived.RevisionId, TemplateLifecycleStatus.Published, TemplateLifecycleStatus.Archived, publishedBy, cancellationToken).ConfigureAwait(false);
        }

        await transitionHook.OnTransitionedAsync(
            draft.RevisionId, TemplateLifecycleStatus.Draft, TemplateLifecycleStatus.Published, publishedBy, cancellationToken).ConfigureAwait(false);

        await cache.RemoveAsync(CacheKey(key), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UnpublishAsync(
        TemplateKey key,
        string unpublishedBy,
        CancellationToken cancellationToken = default)
    {
        await using TemplatingDbContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        List<TemplateRevisionEntity> published = await ctx.TemplateRevisions
            .Where(r => r.TemplateName == key.Name
                        && r.Culture == key.Culture
                        && r.Status == TemplateLifecycleStatus.Published)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (published.Count == 0)
        {
            return; // Idempotent — nothing published to archive
        }

        if (!await transitionHook.CanTransitionAsync(TemplateLifecycleStatus.Published, TemplateLifecycleStatus.Archived, cancellationToken).ConfigureAwait(false))
        {
            throw new TemplateTransitionDeniedException(TemplateLifecycleStatus.Published, TemplateLifecycleStatus.Archived);
        }

        DateTimeOffset now = clock.Now;
        foreach (TemplateRevisionEntity entity in published)
        {
            entity.Status = TemplateLifecycleStatus.Archived;
            entity.ArchivedAt = now;
            entity.ArchivedBy = unpublishedBy;
        }

        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        foreach (TemplateRevisionEntity entity in published)
        {
            await transitionHook.OnTransitionedAsync(
                entity.RevisionId, TemplateLifecycleStatus.Published, TemplateLifecycleStatus.Archived, unpublishedBy, cancellationToken).ConfigureAwait(false);
        }

        await cache.RemoveAsync(CacheKey(key), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task DeleteDraftAsync(
        TemplateKey key,
        string deletedBy,
        CancellationToken cancellationToken = default)
    {
        await using TemplatingDbContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TemplateRevisionEntity? draft = await ctx.TemplateRevisions
            .Where(r => r.TemplateName == key.Name
                        && r.Culture == key.Culture
                        && r.Status == TemplateLifecycleStatus.Draft)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (draft is null)
        {
            throw new NotFoundException(
                $"Cannot delete draft for template '{key.Name}' (culture: {key.Culture ?? "neutral"}): no draft exists.");
        }

        // Only drafts are physically deleted. Published/archived rows are kept (ISO 27001).
        ctx.TemplateRevisions.Remove(draft);
        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<TemplateRevision?> TryGetDraftAsync(
        TemplateKey key, CancellationToken cancellationToken = default)
    {
        await using TemplatingDbContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TemplateRevisionEntity? entity = await ctx.TemplateRevisions
            .Where(r => r.TemplateName == key.Name
                        && r.Culture == key.Culture
                        && r.Status == TemplateLifecycleStatus.Draft)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return entity is null ? null : ToRevision(entity);
    }

    /// <inheritdoc/>
    public async Task<PagedTemplateResult> ListTemplatesAsync(
        TemplateListFilter filter, CancellationToken cancellationToken = default)
    {
        await using TemplatingDbContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Exclude archived revisions from the list — they are only visible in history.
        IQueryable<TemplateRevisionEntity> query = ctx.TemplateRevisions
            .Where(r => r.Status != TemplateLifecycleStatus.Archived);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.Where(r => r.TemplateName.Contains(filter.Search));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(r => r.Status == filter.Status.Value);
        }

        if (filter.Culture is not null)
        {
            query = query.Where(r => r.Culture == filter.Culture);
        }

        if (filter.CategoryId.HasValue)
        {
            query = query.Where(r => r.CategoryId == filter.CategoryId.Value);
        }

        // Group by (Name, Culture) to produce one row per template key.
        var grouped = query
            .GroupBy(r => new { r.TemplateName, r.Culture })
            .Select(g => new
            {
                g.Key.TemplateName,
                g.Key.Culture,
                MimeType = g.OrderByDescending(r => r.CreatedAt).Select(r => r.MimeType).First(),
                CurrentStatus = g.Any(r => r.Status == TemplateLifecycleStatus.Draft)
                    ? TemplateLifecycleStatus.Draft
                    : TemplateLifecycleStatus.Published,
                LastModifiedAt = g.Max(r => r.CreatedAt),
                LastModifiedBy = g.OrderByDescending(r => r.CreatedAt).Select(r => r.CreatedBy).First(),
                HasPublishedVersion = g.Any(r => r.Status == TemplateLifecycleStatus.Published),
                LayoutName = g.OrderByDescending(r => r.CreatedAt).Select(r => r.LayoutName).First(),
            });

        int totalCount = await grouped.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await grouped
            .OrderBy(g => g.TemplateName)
            .ThenBy(g => g.Culture)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        List<TemplateSummary> summaries = items.ConvertAll(g => new TemplateSummary
        {
            Name = g.TemplateName,
            Culture = g.Culture,
            MimeType = g.MimeType,
            CurrentStatus = g.CurrentStatus,
            LastModifiedAt = g.LastModifiedAt,
            LastModifiedBy = g.LastModifiedBy,
            HasPublishedVersion = g.HasPublishedVersion,
            LayoutName = g.LayoutName,
        });

        return new PagedTemplateResult(summaries, totalCount);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TemplateRevision>> GetHistoryAsync(
        TemplateKey key, CancellationToken cancellationToken = default)
    {
        await using TemplatingDbContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await ctx.TemplateRevisions
            .Where(r => r.TemplateName == key.Name && r.Culture == key.Culture)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new TemplateRevision
            {
                RevisionId = r.RevisionId,
                Content = r.Content,
                MimeType = r.MimeType,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                CreatedBy = r.CreatedBy,
                PublishedAt = r.PublishedAt,
                PublishedBy = r.PublishedBy,
                LayoutName = r.LayoutName,
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    private static TemplateRevision ToRevision(TemplateRevisionEntity entity) =>
        new()
        {
            RevisionId = entity.RevisionId,
            Content = entity.Content,
            MimeType = entity.MimeType,
            Status = entity.Status,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            PublishedAt = entity.PublishedAt,
            PublishedBy = entity.PublishedBy,
            LayoutName = entity.LayoutName,
        };

    private string CacheKey(TemplateKey key)
    {
        string tenantSegment = currentTenant is { IsAvailable: true } ? currentTenant.Id?.ToString() ?? "global" : "global";
        return $"granit:tmpl:{tenantSegment}:{key.Name}|{key.Culture ?? string.Empty}";
    }
}
