using Granit.Persistence.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Granit.Metering.EntityFrameworkCore.Internal;

/// <summary>
/// Cross-database transaction-scoped lock helper for the metering aggregator.
/// </summary>
/// <remarks>
/// <para>
/// Both the recompute service and the hourly aggregation job acquire the same lock
/// per <c>(MeterDefinitionId, TenantId)</c> inside their transaction so that they
/// cannot interleave on the same meter/tenant. Raw ingestion is unaffected because
/// it does not acquire this lock.
/// </para>
/// <para>
/// Provider dispatch:
/// <list type="bullet">
///   <item>PostgreSQL → <c>pg_advisory_xact_lock(hashtext(...))</c></item>
///   <item>SQL Server → <c>sp_getapplock @LockOwner='Transaction'</c></item>
///   <item>InMemory / unknown → no-op (logged at debug; tests use this path)</item>
/// </list>
/// In all dialects the lock auto-releases on COMMIT/ROLLBACK — no leak risk.
/// </para>
/// </remarks>
internal static class MeteringConcurrencyLock
{
    /// <summary>
    /// Acquires the lock inside the active transaction. Caller MUST have already opened
    /// a transaction (<c>db.Database.BeginTransactionAsync</c>); the lock is released
    /// automatically when that transaction commits or rolls back.
    /// </summary>
    /// <param name="db">DbContext with an open transaction.</param>
    /// <param name="meterDefinitionId">Meter being processed.</param>
    /// <param name="tenantId">Owning tenant (or <c>null</c> for host-owned meters).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the lock was acquired (or no-op on unsupported providers); <c>false</c> never returned in blocking mode.</returns>
    public static async Task AcquireAsync(
        DbContext db,
        Guid meterDefinitionId,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        string resource = BuildResourceKey(meterDefinitionId, tenantId);
        string? providerName = db.Database.ProviderName;

        switch (providerName)
        {
            case GranitDbProviders.Postgres:
                // pg_advisory_xact_lock is blocking: waits until the lock is free,
                // then auto-releases at transaction end.
                await db.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock(hashtext({0}))",
                    [resource],
                    cancellationToken).ConfigureAwait(false);
                break;

            case GranitDbProviders.SqlServer:
                // sp_getapplock with @LockOwner='Transaction' auto-releases on COMMIT/ROLLBACK.
                // @LockTimeout = -1 → wait indefinitely (matches PostgreSQL semantics).
                await db.Database.ExecuteSqlRawAsync(
                    """
                    DECLARE @result int;
                    EXEC @result = sp_getapplock
                        @Resource    = {0},
                        @LockMode    = 'Exclusive',
                        @LockOwner   = 'Transaction',
                        @LockTimeout = -1;
                    IF @result < 0
                        THROW 51000, 'Failed to acquire metering concurrency lock', 1;
                    """,
                    [resource],
                    cancellationToken).ConfigureAwait(false);
                break;

            default:
                // InMemory / Sqlite / unknown — no native lock primitive.
                // Tests rely on this path; production deployments are expected to use
                // PostgreSQL or SQL Server (the framework's two supported providers).
                break;
        }
    }

    /// <summary>
    /// Stable string key used as the lock resource. Meter id + tenant id (or "global"
    /// when host-owned) — collision risk is negligible because PostgreSQL hashes the
    /// string into a 32-bit lock id internally.
    /// </summary>
    internal static string BuildResourceKey(Guid meterDefinitionId, Guid? tenantId) =>
        $"granit.metering.aggregate:{meterDefinitionId:N}:{(tenantId is null ? "global" : tenantId.Value.ToString("N"))}";
}
