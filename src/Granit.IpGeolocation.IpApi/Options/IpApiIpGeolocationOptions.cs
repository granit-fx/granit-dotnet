namespace Granit.IpGeolocation.IpApi.Options;

/// <summary>
/// Configuration for the opt-in ipinfo.io HTTP geolocation provider, bound from <c>"IpGeolocation:IpApi"</c>.
/// </summary>
/// <remarks>
/// This provider sends the client IP address to an external processor (ipinfo.io). Under GDPR this is a
/// transfer of personal data to a sub-processor, so the provider only acts when explicitly registered and
/// listed in <c>IpGeolocation:ProviderOrder</c>. Keep <see cref="BaseAddress"/> on HTTPS.
/// </remarks>
public sealed class IpApiIpGeolocationOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "IpGeolocation:IpApi";

    /// <summary>Identifier this provider registers under, for <c>IpGeolocation:ProviderOrder</c>.</summary>
    public string ProviderName { get; set; } = "IpApi";

    /// <summary>
    /// ipinfo.io API token. Sent as a <c>Bearer</c> header (never in the URL). Optional, but the tokenless tier
    /// is heavily rate-limited.
    /// </summary>
    public string? ApiToken { get; set; }

    /// <summary>API base address. Defaults to <c>https://ipinfo.io</c>.</summary>
    public Uri BaseAddress { get; set; } = new("https://ipinfo.io");

    /// <summary>Per-request timeout. Defaults to 3 seconds.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Upper bound on the response body buffered from the external API, in bytes. A response from a third party
    /// is untrusted input re-entering the process; capping it bounds memory use even if the endpoint is
    /// compromised or proxied. Defaults to 64 KiB — the ipinfo.io payload is well under 1 KiB.
    /// </summary>
    public long MaxResponseSizeBytes { get; set; } = 64 * 1024;
}
