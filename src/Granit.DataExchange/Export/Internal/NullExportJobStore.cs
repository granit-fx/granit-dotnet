using Granit.DataExchange.Export.Domain;
using Granit.QueryEngine;

namespace Granit.DataExchange.Export.Internal;

/// <summary>
/// Null-object implementation of <see cref="IExportJobReader"/> and <see cref="IExportJobWriter"/>.
/// Stores jobs in memory (non-durable). Replaced by EF Core implementation when
/// <c>Granit.DataExchange.EntityFrameworkCore</c> is installed.
/// </summary>
internal sealed class NullExportJobStore : IExportJobReader, IExportJobWriter
{
    public Task<ExportJob?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult<ExportJob?>(null);

    public Task<PagedResult<ExportJob>> ListAsync(
        ExportJobStatus? status = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
        Task.FromResult(new PagedResult<ExportJob>([], 0, HasMore: false));

    public Task CreateAsync(ExportJob job, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task UpdateAsync(ExportJob job, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
