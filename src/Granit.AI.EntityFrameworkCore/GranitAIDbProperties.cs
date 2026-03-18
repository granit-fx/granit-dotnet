namespace Granit.AI.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the AI EF Core module.
/// </summary>
/// <remarks>
/// <para>
/// These properties control the table prefix and schema used by all entity configurations
/// in <c>Granit.AI.EntityFrameworkCore</c>. Both the internal
/// <c>AIDbContext</c> and the host's <c>Configure*Module()</c> call read
/// the same static values, ensuring migration-time and runtime SQL stay in sync.
/// </para>
/// <para>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled <see cref="Microsoft.EntityFrameworkCore.Metadata.IModel"/>
/// after first use — later mutations have no effect.
/// </para>
/// </remarks>
public static class GranitAIDbProperties
{
    /// <summary>
    /// Table name prefix for all AI tables. Default: <c>"ai_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "ai_";

    /// <summary>
    /// Database schema for all AI tables. Default: <c>null</c> (provider default schema).
    /// </summary>
    public static string? DbSchema { get; set; }
}
