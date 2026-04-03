namespace Granit.Subscriptions.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Subscriptions EF Core module.
/// </summary>
public static class GranitSubscriptionsDbProperties
{
    /// <summary>Table name prefix. Default: <c>"subscriptions_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "subscriptions_";

    /// <summary>Database schema. Default: <c>null</c> (provider default).</summary>
    public static string? DbSchema { get; set; }
}
