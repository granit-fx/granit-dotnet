namespace Granit.DataExchange.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the DataExchange EF Core module.
/// </summary>
/// <remarks>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled model
/// after first use — later mutations have no effect.
/// </remarks>
public static class GranitDataExchangeDbProperties
{
    /// <summary>
    /// Table name prefix for all data exchange tables. Default: <c>"data_exchange_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "data_exchange_";

    /// <summary>
    /// Database schema for all data exchange tables. Default: <c>null</c> (provider default schema).
    /// </summary>
    public static string? DbSchema { get; set; }
}
