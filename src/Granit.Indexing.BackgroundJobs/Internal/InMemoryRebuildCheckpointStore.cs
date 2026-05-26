using System.Collections.Concurrent;

namespace Granit.Indexing.BackgroundJobs.Internal;

/// <summary>
/// In-memory <see cref="IRebuildCheckpointStore{TKey}"/>. Default fallback when no
/// persistent backend is registered.
/// </summary>
/// <remarks>
/// State is lost on process restart, so this is suitable for dev / single-process
/// scenarios only. Production multi-instance hosts wire the EF store via
/// <c>Granit.Indexing.EntityFrameworkCore.Extensions.ServiceCollectionExtensions.AddGranitIndexingEntityFrameworkCoreCheckpointStore</c>.
/// </remarks>
internal sealed class InMemoryRebuildCheckpointStore<TKey> : IRebuildCheckpointStore<TKey>
{
    private readonly ConcurrentDictionary<string, TKey> _store = new(StringComparer.Ordinal);

    public Task<TKey?> GetLastCheckpointAsync(
        Guid? tenantId,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceName);
        cancellationToken.ThrowIfCancellationRequested();
        string key = BuildKey(tenantId, sourceName);
        return Task.FromResult(_store.TryGetValue(key, out TKey? value) ? value : default);
    }

    public Task SetCheckpointAsync(
        Guid? tenantId,
        string sourceName,
        TKey checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceName);
        ArgumentNullException.ThrowIfNull(checkpoint);
        cancellationToken.ThrowIfCancellationRequested();
        _store[BuildKey(tenantId, sourceName)] = checkpoint;
        return Task.CompletedTask;
    }

    public Task ClearAsync(
        Guid? tenantId,
        string sourceName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(sourceName);
        cancellationToken.ThrowIfCancellationRequested();
        _store.TryRemove(BuildKey(tenantId, sourceName), out _);
        return Task.CompletedTask;
    }

    private static string BuildKey(Guid? tenantId, string sourceName) =>
        $"{tenantId?.ToString("N") ?? "global"}|{sourceName}";
}
