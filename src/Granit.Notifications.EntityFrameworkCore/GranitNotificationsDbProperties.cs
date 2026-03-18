namespace Granit.Notifications.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Notifications EF Core module.
/// </summary>
/// <remarks>
/// <para>
/// These properties control the table prefix and schema used by all entity configurations
/// in <c>Granit.Notifications.EntityFrameworkCore</c>. Both the internal
/// <c>NotificationsDbContext</c> and the host's <c>Configure*Module()</c> call read
/// the same static values, ensuring migration-time and runtime SQL stay in sync.
/// </para>
/// <para>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled <see cref="Microsoft.EntityFrameworkCore.Metadata.IModel"/>
/// after first use — later mutations have no effect.
/// </para>
/// </remarks>
public static class GranitNotificationsDbProperties
{
    /// <summary>
    /// Table name prefix for all notification tables. Default: <c>"notification_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "notification_";

    /// <summary>
    /// Database schema for all notification tables. Default: <c>null</c> (provider default schema).
    /// </summary>
    public static string? DbSchema { get; set; }
}
