using Granit.Persistence.EntityFrameworkCore;

namespace Granit.EntityMerge.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the EntityMerge EF Core module.
/// </summary>
/// <remarks>
/// <para>
/// The merge orchestrator owns a single host-level bookkeeping table
/// (<c>merge_idempotency</c>). Both the isolated <c>EntityMergeDbContext</c> and host
/// applications that fold the table into their own <c>DbContext</c> via
/// <c>modelBuilder.ConfigureEntityMergeModule()</c> read these same static values, ensuring
/// migration-time and runtime SQL stay in sync.
/// </para>
/// <para>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled <see cref="Microsoft.EntityFrameworkCore.Metadata.IModel"/>
/// after first use — later mutations have no effect.
/// </para>
/// </remarks>
public static class GranitEntityMergeDbProperties
{
    /// <summary>
    /// Table name prefix for the EntityMerge tables. Default: <c>"merge_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "merge_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema for the merge-orchestrator host-level table.
    /// Falls back to <see cref="GranitDbDefaults.HostDbSchema"/>, then
    /// <see cref="GranitDbDefaults.DbSchema"/> when not explicitly set.
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.HostDbSchema ?? GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
