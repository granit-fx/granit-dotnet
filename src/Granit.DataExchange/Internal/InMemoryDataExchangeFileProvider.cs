using System.Collections.Concurrent;

namespace Granit.DataExchange.Internal;

/// <summary>
/// In-memory implementation of <see cref="IDataExchangeFileProvider"/>.
/// Registered as the default fallback — suitable for tests, CLI tools,
/// and minimal setups without blob storage.
/// </summary>
/// <remarks>
/// Files are stored in a <see cref="ConcurrentDictionary{TKey,TValue}"/> keyed by
/// a generated GUID reference. Data is lost on process restart. For production
/// workloads, register <c>Granit.DataExchange.BlobStorage</c> or a custom implementation.
/// </remarks>
internal sealed class InMemoryDataExchangeFileProvider : IDataExchangeFileProvider
{
    private readonly ConcurrentDictionary<string, byte[]> _store = new(StringComparer.Ordinal);
    private long _counter;

    /// <inheritdoc/>
    public Task<Stream> OpenAsync(string blobReference, CancellationToken cancellationToken = default)
    {
        if (!_store.TryGetValue(blobReference, out byte[]? data))
        {
            throw new FileNotFoundException(
                $"Blob reference '{blobReference}' not found in the in-memory store.");
        }

        return Task.FromResult<Stream>(new MemoryStream(data, writable: false));
    }

    /// <inheritdoc/>
    public Task<string> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        using MemoryStream buffer = new();
        content.CopyTo(buffer);

        string reference = $"mem-{Interlocked.Increment(ref _counter)}";
        _store[reference] = buffer.ToArray();

        return Task.FromResult(reference);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(string blobReference, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(blobReference, out _);
        return Task.CompletedTask;
    }
}
