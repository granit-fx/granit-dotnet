using System.Collections.Concurrent;

namespace Granit.Wolverine.ClaimCheck.Internal;

/// <summary>
/// In-memory implementation of <see cref="IClaimCheckStore"/> for development and testing.
/// </summary>
/// <remarks>
/// <para>
/// Payloads are stored in a <see cref="ConcurrentDictionary{TKey,TValue}"/> and are
/// <b>not shared across instances</b>. In multi-pod deployments, each pod has its own
/// isolated store — use a persistent implementation (blob storage, Redis) in production.
/// </para>
/// <para>
/// Expiry is not enforced. Payloads remain in memory until explicitly deleted or
/// the process is recycled.
/// </para>
/// </remarks>
internal sealed class InMemoryClaimCheckStore : IClaimCheckStore
{
    private readonly ConcurrentDictionary<Guid, byte[]> _store = new();

    /// <inheritdoc/>
    public Task<Guid> StoreAsync(
        ReadOnlyMemory<byte> data,
        string? contentType = null,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
#pragma warning disable GRSEC002 // dev/test-only store: references are opaque and unordered, no UUIDv7/IGuidGenerator need
        var id = Guid.NewGuid();
#pragma warning restore GRSEC002
        _store[id] = data.ToArray();
        return Task.FromResult(id);
    }

    /// <inheritdoc/>
    public Task<byte[]?> RetrieveAsync(
        Guid referenceId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryGetValue(referenceId, out byte[]? data) ? data : null);

    /// <inheritdoc/>
    public Task<bool> DeleteAsync(
        Guid referenceId,
        CancellationToken cancellationToken = default)
        => Task.FromResult(_store.TryRemove(referenceId, out _));
}
