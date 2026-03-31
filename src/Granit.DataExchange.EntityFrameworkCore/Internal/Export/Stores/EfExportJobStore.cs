using Granit.DataExchange.Export;
using Granit.DataExchange.Export.Domain;
using Granit.Persistence;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore.Internal.Export.Stores;

/// <summary>
/// EF Core implementation of <see cref="IExportJobReader"/> and <see cref="IExportJobWriter"/>.
/// Performs CRUD operations on <see cref="ExportJob"/> via <see cref="DataExchangeDbContext"/>.
/// </summary>
internal sealed class EfExportJobStore(
    IDbContextFactory<DataExchangeDbContext> contextFactory)
    : EfStoreBase<ExportJob, DataExchangeDbContext>(contextFactory), IExportJobReader, IExportJobWriter
{
    /// <inheritdoc/>
    public Task<ExportJob?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        FindByIdAsync(id, cancellationToken);

    /// <inheritdoc/>
    public Task<PagedResult<ExportJob>> ListAsync(
        ExportJobStatus? status = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        InlineSpecification<ExportJob> spec = Spec.For<ExportJob>();

        if (status.HasValue)
        {
            spec.Where(j => j.Status == status.Value);
        }

        spec.OrderByDescending(j => (object)j.CreatedAt);

        return PagedAsync(spec, page, pageSize, cancellationToken);
    }

    /// <inheritdoc/>
    public Task CreateAsync(ExportJob job, CancellationToken cancellationToken = default) =>
        AddAsync(job, cancellationToken);

    /// <inheritdoc/>
    public new Task UpdateAsync(ExportJob job, CancellationToken cancellationToken = default) =>
        base.UpdateAsync(job, cancellationToken);
}
