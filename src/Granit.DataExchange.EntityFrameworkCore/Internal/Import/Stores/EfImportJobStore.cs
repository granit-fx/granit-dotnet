using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Pipeline;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Import.Stores;

/// <summary>
/// EF Core implementation of <see cref="IImportJobReader"/> and <see cref="IImportJobWriter"/>.
/// Performs CRUD operations on <see cref="ImportJob"/> via <see cref="DataExchangeDbContext"/>.
/// </summary>
internal sealed class EfImportJobStore(
    IDbContextFactory<DataExchangeDbContext> contextFactory) : IImportJobReader, IImportJobWriter
{
    /// <inheritdoc/>
    public async Task<ImportJob?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using DataExchangeDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return await context.ImportJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<PagedResult<ImportJob>> ListAsync(
        ImportJobStatus? status = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        await using DataExchangeDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        IQueryable<ImportJob> query = context.ImportJobs.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(j => j.Status == status.Value);
        }

        int totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        List<ImportJob> items = await query
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<ImportJob>(items, totalCount, HasMore: (page - 1) * pageSize + items.Count < totalCount);
    }

    /// <inheritdoc/>
    public async Task CreateAsync(ImportJob job, CancellationToken cancellationToken = default)
    {
        await using DataExchangeDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.ImportJobs.Add(job);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task UpdateAsync(ImportJob job, CancellationToken cancellationToken = default)
    {
        await using DataExchangeDbContext context = await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        context.ImportJobs.Update(job);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
