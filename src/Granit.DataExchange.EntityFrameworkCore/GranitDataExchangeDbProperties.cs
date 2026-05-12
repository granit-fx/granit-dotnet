using Granit.Persistence.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the DataExchange EF Core module.
/// </summary>
/// <remarks>
/// <para>
/// DataExchange is a <b>dual-scope</b> module: it serves both host-admin operations
/// (cross-tenant export/import for platform administrators) and tenant-scoped operations.
/// Tables live in the <b>host schema</b> and tenant isolation is enforced via the
/// <c>TenantId</c> row-level query filter (with <c>EfStoreBase</c> bypass for host context).
/// </para>
/// <para>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled model
/// after first use — later mutations have no effect.
/// </para>
/// </remarks>
public static class GranitDataExchangeDbProperties
{
    /// <summary>
    /// Table name prefix for all data exchange tables. Default: <c>"data_exchange_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "data_exchange_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema for DataExchange tables. Falls back to
    /// <see cref="GranitDbDefaults.HostDbSchema"/>, then <see cref="GranitDbDefaults.DbSchema"/>
    /// when not explicitly set — DataExchange tables intentionally live in the host schema
    /// (row-level multi-tenancy via <c>TenantId</c> filter).
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.HostDbSchema ?? GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
