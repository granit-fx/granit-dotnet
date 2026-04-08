using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using Granit.MultiTenancy.Stores;
using Granit.Persistence.EntityFrameworkCore.Migrations;

namespace Granit.MultiTenancy.EntityFrameworkCore.Internal;

/// <summary>
/// Enumerates active tenant IDs from the EF Core tenant store for use during
/// per-tenant migrations (<c>SchemaPerTenant</c> / <c>DatabasePerTenant</c>).
/// </summary>
/// <remarks>
/// On cold start (first deploy), the <c>tenants</c> table may not exist yet.
/// Only <see cref="DbException"/> with a provider-specific "table not found"
/// error is caught (PostgreSQL <c>42P01</c>, SQL Server <c>208</c>).
/// Network errors, auth failures, and other exceptions propagate to fail fast.
/// </remarks>
internal sealed class EfCoreTenantEnumerator(ITenantReader tenantReader) : ITenantEnumerator
{
    public async IAsyncEnumerable<Guid> GetActiveTenantIdsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IReadOnlyList<TenantData> tenants;
        try
        {
            tenants = await tenantReader.GetAllAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (IsTableNotFoundError(ex))
        {
            // Cold start: tenants table doesn't exist yet (first deploy).
            // Yield nothing — host migrations will create the table, then
            // data seeding will create tenants and trigger provisioning.
            yield break;
        }

        foreach (TenantData tenant in tenants.Where(static t => t.IsActive))
        {
            yield return tenant.Id;
        }
    }

    /// <summary>
    /// Detects "table/relation does not exist" errors across database providers.
    /// PostgreSQL: SqlState = 42P01 (undefined_table).
    /// SQL Server: Number = 208 (invalid object name).
    /// </summary>
    private static bool IsTableNotFoundError(DbException ex) =>
        ex.SqlState == "42P01" || // PostgreSQL: undefined_table
        ex.ErrorCode == 208;      // SQL Server: invalid object name
}
