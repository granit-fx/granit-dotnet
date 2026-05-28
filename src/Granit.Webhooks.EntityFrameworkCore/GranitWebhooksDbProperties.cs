using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Webhooks EF Core module.
/// </summary>
/// <remarks>
/// <para>
/// Webhooks is a <b>dual-scope</b> module: platform subscriptions (host admins observing
/// tenant events) coexist with tenant-defined webhooks. Tables live in the <b>host schema</b>
/// and tenant isolation is enforced via the <c>TenantId</c> row-level query filter (with
/// <c>EfStoreBase</c> bypass for host context).
/// </para>
/// <para>
/// These properties control the table prefix and schema used by all entity configurations
/// in <c>Granit.Webhooks.EntityFrameworkCore</c>. Both the internal
/// <c>WebhooksHostDbContext</c> and the host's <c>Configure*Module()</c> call read
/// the same static values, ensuring migration-time and runtime SQL stay in sync.
/// </para>
/// <para>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled <see cref="Microsoft.EntityFrameworkCore.Metadata.IModel"/>
/// after first use — later mutations have no effect.
/// </para>
/// </remarks>
public static class GranitWebhooksDbProperties
{
    /// <summary>
    /// Table name prefix for all webhooks tables. Default: <c>"webhooks_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "webhooks_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema for Webhooks tables. Falls back to
    /// <see cref="GranitDbDefaults.HostDbSchema"/>, then <see cref="GranitDbDefaults.DbSchema"/>
    /// when not explicitly set — Webhooks tables intentionally live in the host schema
    /// (row-level multi-tenancy via <c>TenantId</c> filter).
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.HostDbSchema ?? GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
