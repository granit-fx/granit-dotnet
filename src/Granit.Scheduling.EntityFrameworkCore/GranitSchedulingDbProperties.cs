namespace Granit.Scheduling.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Scheduling EF Core module.
/// </summary>
/// <remarks>
/// <para>
/// These properties control the table prefix and schema used by all entity configurations
/// in <c>Granit.Scheduling.EntityFrameworkCore</c>. Both the internal
/// <c>SchedulingDbContext</c> and the host's <c>ConfigureSchedulingModule()</c> call read
/// the same static values, ensuring migration-time and runtime SQL stay in sync.
/// </para>
/// <para>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled <see cref="Microsoft.EntityFrameworkCore.Metadata.IModel"/>
/// after first use — later mutations have no effect.
/// </para>
/// </remarks>
public static class GranitSchedulingDbProperties
{
    /// <summary>
    /// Table name prefix for all scheduling tables. Default: <c>"scheduling_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "scheduling_";

    /// <summary>
    /// Database schema for all scheduling tables. Default: <c>null</c> (provider default schema).
    /// </summary>
    public static string? DbSchema { get; set; }
}
