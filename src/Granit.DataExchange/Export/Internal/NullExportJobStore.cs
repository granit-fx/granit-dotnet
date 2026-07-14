using Granit.DataExchange.Export.Domain;
using Granit.QueryEngine;

namespace Granit.DataExchange.Export.Internal;

/// <summary>
/// Default implementation of <see cref="IExportJobReader"/> and <see cref="IExportJobWriter"/>.
/// Throws <see cref="NotImplementedException"/> — requires <c>Granit.DataExchange.EntityFrameworkCore</c>.
/// </summary>
internal sealed class NullExportJobStore : IExportJobReader, IExportJobWriter
{
    private const string Message =
        "Export job persistence requires Granit.DataExchange.EntityFrameworkCore. " +
        "Call builder.AddGranitDataExchangeEntityFrameworkCore() to register a concrete IExportJobReader/IExportJobWriter.";

    /// <inheritdoc/>
    public Task<ExportJob?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task<PagedResult<ExportJob>> ListAsync(
        ExportJobStatus? status = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task CreateAsync(ExportJob job, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task UpdateAsync(ExportJob job, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);
}
