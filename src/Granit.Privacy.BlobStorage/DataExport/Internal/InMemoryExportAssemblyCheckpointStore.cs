using System.Collections.Concurrent;
using Granit.Privacy.DataExport;

namespace Granit.Privacy.BlobStorage.DataExport.Internal;

/// <summary>
/// In-memory <see cref="IExportAssemblyCheckpointStore"/>. Default registration when
/// no persistent backend is wired. State is lost on process restart, so a crashed
/// assembly cannot resume — production multi-instance hosts wire the EF impl via
/// <c>Granit.Privacy.EntityFrameworkCore</c> (shipped in a follow-up sub-PR).
/// </summary>
internal sealed class InMemoryExportAssemblyCheckpointStore : IExportAssemblyCheckpointStore
{
    private readonly ConcurrentDictionary<string, ExportAssemblyCheckpoint> _store = new(StringComparer.Ordinal);

    public Task<ExportAssemblyCheckpoint?> GetAsync(
        Guid requestId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.TryGetValue(BuildKey(requestId, tenantId), out ExportAssemblyCheckpoint? cp)
            ? cp
            : null);
    }

    public Task SetAsync(
        Guid requestId,
        Guid? tenantId,
        ExportAssemblyCheckpoint checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        cancellationToken.ThrowIfCancellationRequested();
        _store[BuildKey(requestId, tenantId)] = checkpoint;
        return Task.CompletedTask;
    }

    public Task ClearAsync(
        Guid requestId,
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _store.TryRemove(BuildKey(requestId, tenantId), out _);
        return Task.CompletedTask;
    }

    private static string BuildKey(Guid requestId, Guid? tenantId) =>
        $"{tenantId?.ToString("N") ?? "global"}|{requestId:N}";
}
