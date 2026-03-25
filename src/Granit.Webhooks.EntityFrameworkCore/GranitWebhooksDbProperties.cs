namespace Granit.Webhooks.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Webhooks EF Core module.
/// </summary>
/// <remarks>
/// <para>
/// These properties control the table prefix and schema used by all entity configurations
/// in <c>Granit.Webhooks.EntityFrameworkCore</c>. Both the internal
/// <c>WebhooksDbContext</c> and the host's <c>Configure*Module()</c> call read
/// the same static values, ensuring migration-time and runtime SQL stay in sync.
/// </para>
/// <para>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled <see cref="Microsoft.EntityFrameworkCore.Metadata.IModel"/>
/// after first use — later mutations have no effect.
/// </para>
/// </remarks>
public static class GranitWebhooksDbProperties
{
    /// <summary>
    /// Table name prefix for all webhooks tables. Default: <c>"webhooks_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "webhooks_";

    /// <summary>
    /// Database schema for all webhook tables. Default: <c>null</c> (provider default schema).
    /// </summary>
    public static string? DbSchema { get; set; }
}
