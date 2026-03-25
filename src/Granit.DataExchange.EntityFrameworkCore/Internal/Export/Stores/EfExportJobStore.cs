using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Domain;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Export.Stores;

/// <summary>
/// EF Core implementation of <see cref="IExportJobReader"/> and <see cref="IExportJobWriter"/>.
/// Performs CRUD operations on <see cref="ExportJob"/> via <see cref="DataExchangeDbContext"/>.
/// </summary>
internal sealed class EfExportJobStore(
    IDbContextFactory<DataExchangeDbContext> contextFactory) : IExportJobReader, IExportJobWriter
{
    /// <inheritdoc/>
    public async Task<ExportJob?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using DataExchangeDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.ExportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<ExportJob>> ListAsync(
        ExportJobStatus? status = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        await using DataExchangeDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<ExportJob> query = context.ExportJobs.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(j => j.Status == status.Value);
        }

        int totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        List<ExportJob> items = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<ExportJob>(items, totalCount, HasMore: (page - 1) * pageSize + items.Count < totalCount);
    }

    /// <inheritdoc/>
    public async Task CreateAsync(ExportJob job, CancellationToken cancellationToken = default)
    {
        await using DataExchangeDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.ExportJobs.Add(job);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(ExportJob job, CancellationToken cancellationToken = default)
    {
        await using DataExchangeDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.ExportJobs.Update(job);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
