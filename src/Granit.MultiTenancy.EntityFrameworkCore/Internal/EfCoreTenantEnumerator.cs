using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using Granit.MultiTenancy.Stores;
using Granit.Persistence.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.MultiTenancy.EntityFrameworkCore.Internal;

/// <summary>
/// Enumerates active tenant IDs from the EF Core tenant store for use during
/// per-tenant migrations (<c>SchemaPerTenant</c> / <c>DatabasePerTenant</c>).
/// </summary>
/// <remarks>
/// <para>
/// Registered as <b>singleton</b> (replacing <c>NullTenantEnumerator</c>).
/// Uses <see cref="IServiceScopeFactory"/> to resolve the scoped
/// <see cref="ITenantReader"/> on each call, avoiding the
/// "Cannot consume scoped service from singleton" DI validation error.
/// </para>
/// <para>
/// On cold start (first deploy), the <c>tenants</c> table may not exist yet.
/// Only <see cref="DbException"/> with provider-specific "table not found"
/// errors is caught — network errors and auth failures propagate to fail fast.
/// </para>
/// </remarks>
internal sealed class EfCoreTenantEnumerator(IServiceScopeFactory scopeFactory) : ITenantEnumerator
{
    public async IAsyncEnumerable<Guid> GetActiveTenantIdsAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        IReadOnlyList<TenantData> tenants;
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            ITenantReader tenantReader = scope.ServiceProvider.GetRequiredService<ITenantReader>();
            tenants = await tenantReader.GetAllAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbException ex) when (IsTableNotFoundError(ex))
        {
            // Cold start: tenants table doesn't exist yet (first deploy).
            yield break;
        }

        foreach (TenantData tenant in tenants.Where(static t => t.Activated))
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
