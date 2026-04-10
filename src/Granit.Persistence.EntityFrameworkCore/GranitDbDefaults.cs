namespace Granit.Persistence.EntityFrameworkCore;

/// <summary>
/// Global fallback for database table naming. Individual module
/// <c>*DbProperties</c> classes inherit these defaults unless explicitly
/// overridden. Set these properties at application startup, before
/// <c>ConfigureServices</c> completes — EF Core caches the compiled model
/// after first use.
/// </summary>
/// <remarks>
/// <para><b>SharedDatabase mode</b>: set <see cref="DbSchema"/> to group all
/// module tables into a single schema (e.g. <c>"myapp"</c>). Both host and
/// tenant modules inherit it.</para>
/// <para><b>SchemaPerTenant mode</b>: set <see cref="HostDbSchema"/> for
/// host modules (identity, audit, tenants, jobs…). Leave <see cref="DbSchema"/>
/// <c>null</c> so tenant-isolated tables remain unqualified — required for
/// <c>SET search_path TO tenant_{id}</c> to work correctly.</para>
/// <para>Individual <c>*DbProperties.DbSchema</c> always takes precedence
/// when explicitly assigned (including explicit <c>null</c>).</para>
/// </remarks>
public static class GranitDbDefaults
{
    /// <summary>
    /// Default database schema for all modules.
    /// Host modules fall back to <see cref="HostDbSchema"/> first, then this value.
    /// Tenant modules fall back to this value directly.
    /// <c>null</c> means provider default schema (e.g. <c>public</c> in PostgreSQL).
    /// </summary>
    public static string? DbSchema { get; set; }

    /// <summary>
    /// Database schema for host-level modules (identity, audit, tenants, background
    /// jobs, features, settings, BFF sessions…). When set, host module
    /// <c>*DbProperties</c> use this instead of <see cref="DbSchema"/>.
    /// Configurable via <c>TenantIsolation:HostSchema</c> in <c>appsettings.json</c>.
    /// </summary>
    public static string? HostDbSchema { get; set; }

    /// <summary>
    /// Sets <see cref="HostDbSchema"/> from <c>TenantIsolation:HostSchema</c> in
    /// configuration if not already set. Idempotent — first call wins.
    /// </summary>
    /// <remarks>
    /// Call from module extension methods that have access to <see cref="Microsoft.Extensions.Configuration.IConfiguration"/>
    /// (typically via <c>IHostApplicationBuilder.Configuration</c>). This must run
    /// before any EF Core model compilation — EF Core caches the compiled model
    /// after first use, making later mutations ineffective.
    /// </remarks>
    /// <param name="configuration">The application configuration.</param>
    public static void EnsureFromConfiguration(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        if (HostDbSchema is not null)
        {
            System.Diagnostics.Trace.TraceInformation("[GranitDbDefaults] EnsureFromConfiguration SKIPPED — already '{0}'", HostDbSchema);
            return;
        }

        string? hostSchema = configuration["TenantIsolation:HostSchema"];
        System.Diagnostics.Trace.TraceInformation("[GranitDbDefaults] EnsureFromConfiguration read '{0}' from config", hostSchema ?? "(null)");
        if (hostSchema is not null)
        {
            HostDbSchema = hostSchema;
        }
    }

    /// <summary>
    /// Resets all properties to their default values. Test use only.
    /// </summary>
    internal static void ResetToDefaults()
    {
        DbSchema = null;
        HostDbSchema = null;
    }
}
