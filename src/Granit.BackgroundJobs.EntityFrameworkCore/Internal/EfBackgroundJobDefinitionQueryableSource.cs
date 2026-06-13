using Granit.BackgroundJobs.Domain;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.BackgroundJobs.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for <see cref="BackgroundJobDefinition"/>,
/// backing <c>MapGranitQuery&lt;BackgroundJobDefinition&gt;</c> and the analytics runner over
/// <c>BackgroundJobDefinitionQuery</c>.
/// </summary>
/// <remarks>
/// <see cref="BackgroundJobDefinition"/> is not <c>IMultiTenant</c> (recurring job definitions
/// are host-global), so no query-filter bypass is required.
/// </remarks>
internal sealed class EfBackgroundJobDefinitionQueryableSource(
    IDbContextFactory<BackgroundJobsDbContext> contextFactory)
    : IQueryableSource<BackgroundJobDefinition>, IAsyncDisposable, IDisposable
{
    private BackgroundJobsDbContext? _context;

    public IQueryable<BackgroundJobDefinition> GetQueryable()
    {
        _context ??= contextFactory.CreateDbContext();
        return _context.Jobs.AsNoTracking();
    }

    public ValueTask DisposeAsync()
    {
        BackgroundJobsDbContext? context = _context;
        _context = null;
        return context?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _context?.Dispose();
        _context = null;
    }
}
