using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Identity.EntityFrameworkCore;

/// <summary>
/// Configurable table-naming properties for the Identity EF Core store.
/// Pinned in one place so consuming apps' migrations and any custom
/// projection layers stay aligned with the framework's choice.
/// </summary>
/// <remarks>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled model
/// after first use — later mutations have no effect.
/// </remarks>
public static class GranitIdentityDbProperties
{
    /// <summary>
    /// Table-name prefix applied to every Identity-owned table. Default: <c>"identity_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "identity_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// PostgreSQL schema for the Identity tables. Falls back to
    /// <see cref="GranitDbDefaults.HostDbSchema"/>, then
    /// <see cref="GranitDbDefaults.DbSchema"/> when not explicitly set.
    /// <see langword="null"/> means the host decides (single-schema
    /// deployments simply don't set one).
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.HostDbSchema ?? GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
