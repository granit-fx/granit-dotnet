using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine;
using Granit.Templating.EntityFrameworkCore.Entities;
using Granit.Templating.Store;
using Granit.Workflow.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Templating.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for
/// <see cref="TemplateSummary"/>. Groups <see cref="TemplateRevisionEntity"/> rows
/// by <c>(TenantId, TemplateName, Culture)</c> and surfaces the most recent
/// non-archived revision per template.
/// </summary>
/// <remarks>
/// When no tenant context is active (host admin), the multi-tenant query filter is
/// bypassed so all templates are returned cross-tenant. The <see cref="GranitFilterNames.Publishable"/>
/// filter is always bypassed: the list manages the full lifecycle (drafts included).
/// </remarks>
internal sealed class EfTemplateSummaryQueryableSource(
    IDbContextFactory<TemplatingDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : IQueryableSource<TemplateSummary>
{
    private readonly TemplatingDbContext _context = contextFactory.CreateDbContext();
    private readonly bool _bypassTenantFilter = !currentTenant.IsAvailable;

    public IQueryable<TemplateSummary> GetQueryable()
    {
        IQueryable<TemplateRevisionEntity> query = _context.TemplateRevisions
            .AsNoTracking()
            .IgnoreQueryFilters([GranitFilterNames.Publishable])
            .Where(r => r.LifecycleStatus != WorkflowLifecycleStatus.Archived);

        if (_bypassTenantFilter)
        {
            query = query.IgnoreQueryFilters([GranitFilterNames.MultiTenant]);
        }

        return query
            .GroupBy(r => new { r.TenantId, r.TemplateName, r.Culture })
            .Select(g => new TemplateSummary
            {
                TenantId = g.Key.TenantId,
                Name = g.Key.TemplateName,
                Culture = g.Key.Culture,
                MimeType = g.OrderByDescending(r => r.CreatedAt).Select(r => r.MimeType).First(),
                CurrentStatus = g.Any(r => r.LifecycleStatus == WorkflowLifecycleStatus.Draft)
                    ? WorkflowLifecycleStatus.Draft
                    : WorkflowLifecycleStatus.Published,
                LastModifiedAt = g.Max(r => r.CreatedAt),
                LastModifiedBy = g.OrderByDescending(r => r.CreatedAt).Select(r => r.CreatedBy).First()!,
                HasPublishedVersion = g.Any(r => r.IsPublished),
                LayoutName = g.OrderByDescending(r => r.CreatedAt).Select(r => r.LayoutName).First(),
                CategoryId = g.OrderByDescending(r => r.CreatedAt).Select(r => r.CategoryId).First(),
            });
    }
}
