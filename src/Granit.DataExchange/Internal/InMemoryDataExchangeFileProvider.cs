using System.Collections.Concurrent;
using Granit.Domain.ValueObjects;

namespace Granit.DataExchange.Internal;

/// <summary>
/// In-memory implementation of <see cref="IDataExchangeFileProvider"/>.
/// Opt-in via <c>AddInMemoryDataExchangeFileProvider()</c> — suitable for tests
/// and single-process CLI tools.
/// </summary>
/// <remarks>
/// Files are stored in an instance <see cref="ConcurrentDictionary{TKey,TValue}"/>, so the
/// registration MUST be a singleton: a scoped instance would lose every file at scope end
/// (upload and execution run in different scopes). Data is lost on process restart. For
/// production workloads, register <c>Granit.DataExchange.BlobStorage</c> or a custom implementation.
/// </remarks>
internal sealed class InMemoryDataExchangeFileProvider : IDataExchangeFileProvider
{
    private readonly ConcurrentDictionary<string, byte[]> _store = new(StringComparer.Ordinal);
    private long _counter;

    /// <inheritdoc/>
    public Task<Stream> OpenAsync(BlobReference blobReference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(blobReference);

        if (!_store.TryGetValue(blobReference.Value, out byte[]? data))
        {
            throw new FileNotFoundException(
                $"Blob reference '{blobReference.Value}' not found in the in-memory store.");
        }

        return Task.FromResult<Stream>(new MemoryStream(data, writable: false));
    }

    /// <inheritdoc/>
    public async Task<BlobReference> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        await using MemoryStream buffer = new();
        await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);

        string reference = $"mem-{Interlocked.Increment(ref _counter)}";
        _store[reference] = buffer.ToArray();

        return BlobReference.Create(reference);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(BlobReference blobReference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(blobReference);

        _store.TryRemove(blobReference.Value, out _);
        return Task.CompletedTask;
    }
}
