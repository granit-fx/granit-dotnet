namespace Granit.Documents.Renditions.EntityFrameworkCore;

/// <summary>
/// Configurable table-naming properties for the renditions EF Core module. Mirrors the
/// <c>GranitDocumentsDbProperties</c> pattern: hosts set the prefix / schema once at
/// startup before EF Core caches the compiled model.
/// </summary>
public static class GranitRenditionsDbProperties
{
    /// <summary>Table name prefix. Default: <c>"documents_"</c> (shared with the parent module).</summary>
    public static string DbTablePrefix { get; set; } = "documents_";

    /// <summary>
    /// Schema for the renditions table. <c>null</c> means provider default (typically
    /// <c>dbo</c> on SQL Server, <c>public</c> on Postgres). Hosts may override to keep
    /// the renditions table in the same schema as the rest of the documents module.
    /// </summary>
    public static string? DbSchema { get; set; }
}
