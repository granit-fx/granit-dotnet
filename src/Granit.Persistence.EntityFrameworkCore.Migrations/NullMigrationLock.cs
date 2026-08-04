namespace Granit.Persistence.EntityFrameworkCore.Migrations;

/// <summary>
/// No-op migration lock for single-instance deployments and testing.
/// Always succeeds — no distributed coordination.
/// </summary>
/// <remarks>
/// Registered as the <c>TryAdd</c> fallback by <c>AddGranitPersistenceMigrations()</c> and
/// <c>AddGranitMigrateSupport()</c>. Provider packages (<c>AddGranitPostgres()</c>,
/// <c>AddGranitSqlServer()</c>) replace it with a real distributed lock regardless of
/// registration order. When this implementation is resolved outside the Development
/// environment, <c>GranitPersistenceEntityFrameworkCoreMigrationsModule</c> logs a Warning:
/// concurrent replicas would migrate unguarded.
/// </remarks>
public sealed class NullMigrationLock : IGranitMigrationLock
{
    /// <inheritdoc/>
    public Task<IAsyncDisposable?> TryAcquireAsync(string resource, CancellationToken cancellationToken) =>
        Task.FromResult<IAsyncDisposable?>(NullLockHandle.Instance);

    private sealed class NullLockHandle : IAsyncDisposable
    {
        public static readonly NullLockHandle Instance = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
