namespace Granit.IpGeolocation.MaxMind.Options;

/// <summary>
/// Configuration for the offline MaxMind (<c>.mmdb</c>) geolocation provider, bound from
/// <c>"IpGeolocation:MaxMind"</c>.
/// </summary>
/// <remarks>
/// Works with any MaxMind DB format database: MaxMind GeoLite2/GeoIP2 (City or Country) and DB-IP Lite.
/// The database file is supplied and provisioned by the consumer (it is large and separately licensed) — this
/// package never embeds or redistributes it.
/// </remarks>
public sealed class MaxMindIpGeolocationOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "IpGeolocation:MaxMind";

    /// <summary>
    /// Absolute or relative path to the <c>.mmdb</c> file (e.g. <c>/var/lib/geoip/GeoLite2-City.mmdb</c>).
    /// </summary>
    public string DatabasePath { get; set; } = string.Empty;

    /// <summary>
    /// Identifier this provider registers under, for <c>IpGeolocation:ProviderOrder</c>. Defaults to
    /// <c>"MaxMind"</c>; override when running this provider against a DB-IP database alongside a MaxMind one.
    /// </summary>
    public string ProviderName { get; set; } = "MaxMind";

    /// <summary>
    /// When <c>true</c> (default), watches the database file and atomically swaps in a fresh reader when it
    /// changes (e.g. after a weekly update), so the process never serves a stale database or needs a restart.
    /// </summary>
    public bool ReloadOnChange { get; set; } = true;

    /// <summary>How the database file is opened. Defaults to <see cref="MaxMindFileAccess.Memory"/>.</summary>
    public MaxMindFileAccess FileAccess { get; set; } = MaxMindFileAccess.Memory;
}
