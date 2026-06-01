namespace Granit.Hostnames.Options;

/// <summary>
/// Platform-level options for the custom hostname feature.
/// Bind from the <c>"Hostnames"</c> configuration section.
/// </summary>
public sealed class HostnamesOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Hostnames";

    /// <summary>
    /// The platform's ingress CNAME target that customers must point their hostname to.
    /// When set, <c>BeginVerification</c> is called automatically on hostname creation,
    /// so the response immediately carries the DNS records the customer must configure.
    /// Example: <c>"customers.platform.example.com"</c>
    /// </summary>
    public string? IngressTarget { get; set; }

    /// <summary>
    /// DNS label prefix used for the TXT ownership-challenge record.
    /// The full challenge name is <c>{TxtChallengePrefix}.{host}</c>.
    /// Default: <c>"_granit-challenge"</c>.
    /// </summary>
    public string TxtChallengePrefix { get; set; } = "_granit-challenge";

    /// <summary>
    /// Maximum number of hostnames the poller fetches per tick.
    /// Default: <c>100</c>.
    /// </summary>
    public int VerificationBatchSize { get; set; } = 100;
}
