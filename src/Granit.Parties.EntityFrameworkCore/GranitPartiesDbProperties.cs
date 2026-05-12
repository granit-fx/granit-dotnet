using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Parties.EntityFrameworkCore;

/// <summary>Table-naming properties for the Parties EF Core module.</summary>
/// <remarks>
/// Parties is a <b>dual-scope</b> module: it backs SaaS billing (the tenant itself is a
/// <c>Party</c> referenced by <c>Subscription.PartyId</c>) and tenant-scoped CRM data.
/// Tables live in the <b>host schema</b> and tenant isolation is enforced via the
/// <c>TenantId</c> row-level query filter (with <c>EfStoreBase</c> bypass for host context).
/// </remarks>
public static class GranitPartiesDbProperties
{
    /// <summary>Table prefix. Default: <c>"parties_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "parties_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema. Falls back to <see cref="GranitDbDefaults.HostDbSchema"/>,
    /// then <see cref="GranitDbDefaults.DbSchema"/> when not explicitly set.
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.HostDbSchema ?? GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
