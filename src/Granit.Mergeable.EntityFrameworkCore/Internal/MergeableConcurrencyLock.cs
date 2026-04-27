using Microsoft.EntityFrameworkCore;

namespace Granit.Mergeable.EntityFrameworkCore.Internal;

/// <summary>
/// Per-tenant transaction-scoped advisory lock used by the merge orchestrator to serialise
/// concurrent merges on the same tenant. Without it, two admins triggering
/// <c>MergeAsync(survivor, A→B)</c> and <c>MergeAsync(survivor, A→C)</c> in parallel could
/// race on row visibility and produce a corrupted tombstone chain.
/// </summary>
/// <remarks>
/// <para>
/// Provider dispatch (mirrors <c>MeteringConcurrencyLock</c>):
/// <list type="bullet">
///   <item>PostgreSQL → <c>pg_advisory_xact_lock(hashtext(...))</c> — blocking, auto-released on COMMIT/ROLLBACK.</item>
///   <item>SQL Server → <c>sp_getapplock @LockOwner='Transaction'</c> — same semantics.</item>
///   <item>InMemory / unknown → no-op (tests path).</item>
/// </list>
/// </para>
/// <para>
/// The lock granularity is per-tenant (one global lock per host when <paramref name="tenantId"/>
/// is null). Aggregate types share the lock at the tenant level — merging two parties and two
/// invoices simultaneously on the same tenant is unusual and the conservative serialisation
/// is acceptable; can be split per-aggregate-type later if contention proves measurable.
/// </para>
/// </remarks>
internal static class MergeableConcurrencyLock
{
    private const string PostgresProvider = "Npgsql.EntityFrameworkCore.PostgreSQL";
    private const string SqlServerProvider = "Microsoft.EntityFrameworkCore.SqlServer";

    /// <summary>
    /// Acquires the lock inside the active transaction. Caller MUST have an open transaction
    /// (the orchestrator's <c>TransactionScope</c> + <c>BeginTransactionAsync</c>); released on
    /// COMMIT/ROLLBACK.
    /// </summary>
    public static async Task AcquireAsync(
        DbContext db,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        string resource = BuildResourceKey(tenantId);
        string? providerName = db.Database.ProviderName;

        switch (providerName)
        {
            case PostgresProvider:
                await db.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock(hashtext({0}))",
                    [resource],
                    cancellationToken).ConfigureAwait(false);
                break;

            case SqlServerProvider:
                await db.Database.ExecuteSqlRawAsync(
                    """
                    DECLARE @result int;
                    EXEC @result = sp_getapplock
                        @Resource    = {0},
                        @LockMode    = 'Exclusive',
                        @LockOwner   = 'Transaction',
                        @LockTimeout = -1;
                    IF @result < 0
                        THROW 51000, 'Failed to acquire mergeable concurrency lock', 1;
                    """,
                    [resource],
                    cancellationToken).ConfigureAwait(false);
                break;

            default:
                // InMemory / Sqlite / unknown — no native primitive. Tests rely on this path.
                break;
        }
    }

    internal static string BuildResourceKey(Guid? tenantId) =>
        $"granit.mergeable:{(tenantId is null ? "global" : tenantId.Value.ToString("N"))}";
}
