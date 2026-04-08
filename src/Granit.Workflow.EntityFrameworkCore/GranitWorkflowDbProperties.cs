using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Workflow.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Workflow EF Core module.
/// </summary>
/// <remarks>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled model
/// after first use — later mutations have no effect.
/// </remarks>
public static class GranitWorkflowDbProperties
{
    /// <summary>
    /// Table name prefix for all workflow tables. Default: <c>"workflow_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "workflow_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema for tenant-level tables.
    /// Falls back to <see cref="GranitDbDefaults.DbSchema"/> when not explicitly set.
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
