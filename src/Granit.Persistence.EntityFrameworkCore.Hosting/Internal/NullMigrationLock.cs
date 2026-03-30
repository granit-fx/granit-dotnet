namespace Granit.Persistence.EntityFrameworkCore.Hosting.Internal;

/// <summary>
/// No-op migration lock for single-instance deployments and testing.
/// Always succeeds — no distributed coordination.
/// </summary>
internal sealed class NullMigrationLock : IGranitMigrationLock
{
    public Task<IAsyncDisposable?> TryAcquireAsync(string resource, CancellationToken cancellationToken) =>
        Task.FromResult<IAsyncDisposable?>(NullLockHandle.Instance);

    private sealed class NullLockHandle : IAsyncDisposable
    {
        public static readonly NullLockHandle Instance = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
