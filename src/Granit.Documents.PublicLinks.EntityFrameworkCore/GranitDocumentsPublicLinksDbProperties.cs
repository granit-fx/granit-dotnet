namespace Granit.Documents.PublicLinks.EntityFrameworkCore;

/// <summary>
/// Configurable table-naming properties for the public-links EF Core module.
/// Mirrors <c>GranitAssetMetadataDbProperties</c>: hosts set the prefix / schema
/// once at startup before EF Core caches the compiled model.
/// </summary>
public static class GranitDocumentsPublicLinksDbProperties
{
    /// <summary>Table name prefix. Default: <c>"documents_"</c> (shared with the parent module).</summary>
    public static string DbTablePrefix { get; set; } = "documents_";

    /// <summary>
    /// Schema for the public-links table. <c>null</c> means provider default
    /// (typically <c>dbo</c> on SQL Server, <c>public</c> on Postgres).
    /// </summary>
    public static string? DbSchema { get; set; }
}
