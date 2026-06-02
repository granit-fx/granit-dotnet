using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Pipeline;
using Granit.MultiTenancy;
using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Import.Stores;

/// <summary>
/// EF Core implementation of <see cref="IImportJobReader"/> and <see cref="IImportJobWriter"/>.
/// Performs CRUD operations on <see cref="ImportJob"/> via <see cref="DataExchangeDbContext"/>.
/// </summary>
internal sealed class EfImportJobStore(
    IDbContextFactory<DataExchangeDbContext> contextFactory,
    ICurrentTenant currentTenant)
    : EfStoreBase<ImportJob, DataExchangeDbContext>(contextFactory, currentTenant), IImportJobReader, IImportJobWriter
{
    /// <inheritdoc/>
    public Task<ImportJob?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        FindByIdAsync(id, cancellationToken);

    /// <inheritdoc/>
    public Task<PagedResult<ImportJob>> ListAsync(
        ImportJobStatus? status = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        InlineSpecification<ImportJob> spec = Spec.For<ImportJob>();

        if (status.HasValue)
        {
            spec.Where(j => j.Status == status.Value);
        }

        spec.OrderByDescending(j => (object)j.CreatedAt);

        return PagedAsync(spec, page, pageSize, cancellationToken);
    }

    /// <inheritdoc/>
    public Task CreateAsync(ImportJob job, CancellationToken cancellationToken = default) =>
        AddAsync(job, cancellationToken);

    /// <inheritdoc/>
    public new Task UpdateAsync(ImportJob job, CancellationToken cancellationToken = default) =>
        base.UpdateAsync(job, cancellationToken);

    /// <inheritdoc/>
    public Task UpdateAsync(ImportJob job, string concurrencyStamp, CancellationToken cancellationToken = default) =>
        WriteAsync(db =>
        {
            db.Set<ImportJob>().Update(job);
            db.SetConcurrencyStampOriginalValue(job, concurrencyStamp);
            return Task.CompletedTask;
        }, cancellationToken);
}
