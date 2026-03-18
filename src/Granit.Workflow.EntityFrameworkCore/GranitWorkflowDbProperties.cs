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

    /// <summary>
    /// Database schema for all workflow tables. Default: <c>null</c> (provider default schema).
    /// </summary>
    public static string? DbSchema { get; set; }
}
