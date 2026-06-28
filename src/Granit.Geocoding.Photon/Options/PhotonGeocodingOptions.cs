namespace Granit.Geocoding.Photon.Options;

/// <summary>
/// Configuration for the opt-in Photon (<c>photon.komoot.io</c>) geocoding provider, bound from
/// <c>"Geocoding:Photon"</c>.
/// </summary>
/// <remarks>
/// This provider sends the address to an external service. Under GDPR this is a transfer of personal data to a
/// sub-processor, so the provider only acts when explicitly registered and listed in <c>Geocoding:ProviderOrder</c>.
/// Keep <see cref="BaseAddress"/> on HTTPS, and respect <see cref="RateLimitPerSecond"/> — the public komoot
/// endpoint is free but fair-use, so extensive traffic is throttled. Self-host Photon to lift the rate cap.
/// </remarks>
public sealed class PhotonGeocodingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Geocoding:Photon";

    /// <summary>Identifier this provider registers under, for <c>Geocoding:ProviderOrder</c>.</summary>
    public string ProviderName { get; set; } = "Photon";

    /// <summary>API base address. Defaults to <c>https://photon.komoot.io</c>.</summary>
    public Uri BaseAddress { get; set; } = new("https://photon.komoot.io");

    /// <summary>Per-request timeout. Defaults to 5 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Upper bound on the response body buffered from the external API, in bytes. A third-party response is
    /// untrusted input re-entering the process; capping it bounds memory use. Defaults to 256 KiB.
    /// </summary>
    public long MaxResponseSizeBytes { get; set; } = 256 * 1024;

    /// <summary>
    /// Optional <c>User-Agent</c> sent on every request. Photon does not mandate one (unlike Nominatim), but an
    /// identifying agent string (an app name and a contact) is courteous on the shared public endpoint. When left
    /// blank, no <c>User-Agent</c> header is added.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Optional result language passed as the Photon <c>lang</c> parameter (e.g. <c>"en"</c>, <c>"de"</c>,
    /// <c>"fr"</c>). When blank, Photon's default language is used. Only affects the textual properties of the
    /// response, never the coordinate.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Maximum requests per second issued to the endpoint. Defaults to <c>1</c> — a courteous pace for the shared
    /// public <c>photon.komoot.io</c> server. Raise it only when pointing <see cref="BaseAddress"/> at a
    /// self-hosted instance you control.
    /// </summary>
    public double RateLimitPerSecond { get; set; } = 1;
}
