using Granit.Exceptions;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Templating.EntityFrameworkCore.Entities;
using Granit.Templating.Exceptions;
using Granit.Templating.Keys;
using Granit.Templating.Pipeline;
using Granit.Templating.Store;
using Granit.Timing;
using Granit.Workflow.Domain;
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
/// Lifecycle transitions are automatically recorded in <c>WorkflowTransitionRecord</c> by the
/// <c>WorkflowTransitionInterceptor</c>.
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
                                && r.IsPublished)
                    .FirstOrDefaultAsync(innerCt).ConfigureAwait(false);

                return entity is null
                    ? TemplateCacheEntry.NotFound
                    : TemplateCacheEntry.From(entity.Content, entity.MimeType, entity.Id, entity.LayoutName);
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
            .IgnoreQueryFilters([GranitFilterNames.Publishable])
            .Where(r => r.TemplateName == key.Name
                        && r.Culture == key.Culture
                        && r.LifecycleStatus == WorkflowLifecycleStatus.Draft)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (existing is not null)
        {
            // Update existing draft in place (only one draft per key)
            existing.UpdateDraft(content, mimeType, layoutName);
        }
        else
        {
            // Look up the VersionId for existing revisions of this template key
            // so the VersioningInterceptor groups them together.
            Guid? existingVersionId = await ctx.TemplateRevisions
                .IgnoreQueryFilters([GranitFilterNames.Publishable])
                .Where(r => r.TemplateName == key.Name && r.Culture == key.Culture)
                .Select(r => (Guid?)r.VersionId)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            var entity = TemplateRevisionEntity.Create(
                guidGenerator.Create(),
                key.Name,
                key.Culture,
                content,
                mimeType,
                layoutName,
                versionId: existingVersionId);

            entity.CreatedBy = updatedBy;
            entity.CreatedAt = clock.Now;

            ctx.TemplateRevisions.Add(entity);
        }

        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task PublishAsync(
        TemplateKey key,
        string publishedBy,
        CancellationToken cancellationToken = default)
    {
        if (!await transitionHook.CanTransitionAsync(WorkflowLifecycleStatus.Draft, WorkflowLifecycleStatus.Published, cancellationToken).ConfigureAwait(false))
        {
            throw new TemplateTransitionDeniedException(WorkflowLifecycleStatus.Draft, WorkflowLifecycleStatus.Published);
        }

        await using TemplatingDbContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        TemplateRevisionEntity? draft = await ctx.TemplateRevisions
            .IgnoreQueryFilters([GranitFilterNames.Publishable])
            .Where(r => r.TemplateName == key.Name
                        && r.Culture == key.Culture
                        && r.LifecycleStatus == WorkflowLifecycleStatus.Draft)
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
                        && r.IsPublished)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (TemplateRevisionEntity published in currentlyPublished)
        {
            published.Archive();
        }

        DateTimeOffset now = clock.Now;
        draft.Publish(publishedBy, now);

        // WorkflowTransitionInterceptor automatically creates WorkflowTransitionRecord
        // entries for all status changes during SaveChanges.
        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Notify the hook after persistence (post-commit side-effects).
        foreach (TemplateRevisionEntity archived in currentlyPublished)
        {
            await transitionHook.OnTransitionedAsync(
                archived.Id, WorkflowLifecycleStatus.Published, WorkflowLifecycleStatus.Archived,
                publishedBy, cancellationToken).ConfigureAwait(false);
        }

        await transitionHook.OnTransitionedAsync(
            draft.Id, WorkflowLifecycleStatus.Draft, WorkflowLifecycleStatus.Published,
            publishedBy, cancellationToken).ConfigureAwait(false);

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
                        && r.IsPublished)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (published.Count == 0)
        {
            return; // Idempotent — nothing published to archive
        }

        if (!await transitionHook.CanTransitionAsync(WorkflowLifecycleStatus.Published, WorkflowLifecycleStatus.Archived, cancellationToken).ConfigureAwait(false))
        {
            throw new TemplateTransitionDeniedException(WorkflowLifecycleStatus.Published, WorkflowLifecycleStatus.Archived);
        }

        foreach (TemplateRevisionEntity entity in published)
        {
            entity.Archive();
        }

        await ctx.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Notify the hook after persistence (post-commit side-effects).
        foreach (TemplateRevisionEntity entity in published)
        {
            await transitionHook.OnTransitionedAsync(
                entity.Id, WorkflowLifecycleStatus.Published, WorkflowLifecycleStatus.Archived,
                unpublishedBy, cancellationToken).ConfigureAwait(false);
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
            .IgnoreQueryFilters([GranitFilterNames.Publishable])
            .Where(r => r.TemplateName == key.Name
                        && r.Culture == key.Culture
                        && r.LifecycleStatus == WorkflowLifecycleStatus.Draft)
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
            .IgnoreQueryFilters([GranitFilterNames.Publishable])
            .Where(r => r.TemplateName == key.Name
                        && r.Culture == key.Culture
                        && r.LifecycleStatus == WorkflowLifecycleStatus.Draft)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        return entity is null ? null : ToRevision(entity);
    }

    /// <inheritdoc/>
    public async Task<PagedTemplateResult> ListTemplatesAsync(
        TemplateListFilter filter, CancellationToken cancellationToken = default)
    {
        await using TemplatingDbContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        // Exclude archived revisions from the list — they are only visible in history.
        // Bypass the Publishable filter: this query manages the full lifecycle.
        IQueryable<TemplateRevisionEntity> query = ctx.TemplateRevisions
            .IgnoreQueryFilters([GranitFilterNames.Publishable])
            .Where(r => r.LifecycleStatus != WorkflowLifecycleStatus.Archived);

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.Where(r => r.TemplateName.Contains(filter.Search));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(r => r.LifecycleStatus == filter.Status.Value);
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
                CurrentStatus = g.Any(r => r.LifecycleStatus == WorkflowLifecycleStatus.Draft)
                    ? WorkflowLifecycleStatus.Draft
                    : WorkflowLifecycleStatus.Published,
                LastModifiedAt = g.Max(r => r.CreatedAt),
                LastModifiedBy = g.OrderByDescending(r => r.CreatedAt).Select(r => r.CreatedBy).First(),
                HasPublishedVersion = g.Any(r => r.IsPublished),
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
            .IgnoreQueryFilters([GranitFilterNames.Publishable])
            .Where(r => r.TemplateName == key.Name && r.Culture == key.Culture)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new TemplateRevision
            {
                RevisionId = r.Id,
                Content = r.Content,
                MimeType = r.MimeType,
                Status = r.LifecycleStatus,
                Version = r.Version,
                CreatedAt = r.CreatedAt,
                CreatedBy = r.CreatedBy,
                PublishedAt = r.PublishedAt,
                PublishedBy = r.PublishedBy,
                LayoutName = r.LayoutName,
            })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetDistinctLayoutNamesAsync(
        CancellationToken cancellationToken = default)
    {
        await using TemplatingDbContext ctx = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await ctx.TemplateRevisions
            .IgnoreQueryFilters([GranitFilterNames.Publishable])
            .Where(r => r.LayoutName != null)
            .Select(r => r.LayoutName!)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    private static TemplateRevision ToRevision(TemplateRevisionEntity entity) =>
        new()
        {
            RevisionId = entity.Id,
            Content = entity.Content,
            MimeType = entity.MimeType,
            Status = entity.LifecycleStatus,
            Version = entity.Version,
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
