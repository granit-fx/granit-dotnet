using Granit.DataExchange.Import.Domain;
using Granit.DataExchange.Import.Pipeline;
using Granit.QueryEngine;

namespace Granit.DataExchange.Import.Internal;

/// <summary>
/// Default implementation of <see cref="IImportJobReader"/> and <see cref="IImportJobWriter"/>.
/// Throws <see cref="NotImplementedException"/> — requires <c>Granit.DataExchange.EntityFrameworkCore</c>.
/// </summary>
internal sealed class NullImportJobStore : IImportJobReader, IImportJobWriter
{
    private const string Message =
        "Import job persistence requires Granit.DataExchange.EntityFrameworkCore. " +
        "Call builder.AddGranitDataExchangeEntityFrameworkCore() to register a concrete IImportJobReader/IImportJobWriter.";

    /// <inheritdoc/>
    public Task<ImportJob?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task<PagedResult<ImportJob>> ListAsync(
        ImportJobStatus? status = null, int page = 1, int pageSize = 20, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task CreateAsync(ImportJob job, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task UpdateAsync(ImportJob job, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);

    /// <inheritdoc/>
    public Task UpdateAsync(ImportJob job, string concurrencyStamp, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException(Message);
}
