namespace Granit.Geocoding.Nominatim.Options;

/// <summary>
/// Configuration for the opt-in OpenStreetMap Nominatim geocoding provider, bound from <c>"Geocoding:Nominatim"</c>.
/// </summary>
/// <remarks>
/// This provider sends the address to an external service. Under GDPR this is a transfer of personal data to a
/// sub-processor, so the provider only acts when explicitly registered and listed in <c>Geocoding:ProviderOrder</c>.
/// Keep <see cref="BaseAddress"/> on HTTPS, set an identifying <see cref="UserAgent"/> (mandatory under the Nominatim
/// usage policy), and respect <see cref="RateLimitPerSecond"/> — the public endpoint allows at most 1 request/second.
/// </remarks>
public sealed class NominatimGeocodingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Geocoding:Nominatim";

    /// <summary>Identifier this provider registers under, for <c>Geocoding:ProviderOrder</c>.</summary>
    public string ProviderName { get; set; } = "Nominatim";

    /// <summary>API base address. Defaults to <c>https://nominatim.openstreetmap.org</c>.</summary>
    public Uri BaseAddress { get; set; } = new("https://nominatim.openstreetmap.org");

    /// <summary>Per-request timeout. Defaults to 5 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Upper bound on the response body buffered from the external API, in bytes. A third-party response is
    /// untrusted input re-entering the process; capping it bounds memory use. Defaults to 256 KiB.
    /// </summary>
    public long MaxResponseSizeBytes { get; set; } = 256 * 1024;

    /// <summary>
    /// <c>User-Agent</c> sent on every request. <strong>Mandatory</strong>: the Nominatim usage policy requires a
    /// genuine, identifying agent string (an app name and a contact). Requests without one are blocked. No default
    /// is provided, so startup validation fails until it is set.
    /// </summary>
    public string UserAgent { get; set; } = string.Empty;

    /// <summary>
    /// Maximum requests per second issued to the endpoint. Defaults to <c>1</c> — the limit for the shared public
    /// <c>nominatim.openstreetmap.org</c> server. Raise it only when pointing <see cref="BaseAddress"/> at a
    /// self-hosted instance you control.
    /// </summary>
    public double RateLimitPerSecond { get; set; } = 1;
}
