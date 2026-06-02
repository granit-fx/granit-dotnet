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

    /// <summary>
    /// Maximum degree of parallelism for the per-tick DNS verification batch.
    /// Prevents a single slow DNS delegation chain from blocking all other domains.
    /// Default: <c>10</c>.
    /// </summary>
    public int VerificationConcurrency { get; set; } = 10;

    /// <summary>
    /// Per-query DNS resolution timeout. Applied to each individual record lookup.
    /// Default: <c>3</c> seconds.
    /// </summary>
    public int DnsQueryTimeoutSeconds { get; set; } = 3;

    /// <summary>
    /// Shared secret used to validate HMAC-SHA-256 signatures on the
    /// <c>POST /{id}/certificate-status</c> webhook. When <c>null</c> or empty,
    /// signature verification is skipped (useful for local development).
    /// Set via <c>Hostnames:CertificateWebhookSecret</c> — use a Vault secret or
    /// environment variable, never a hardcoded value.
    /// </summary>
    public string? CertificateWebhookSecret { get; set; }

    /// <summary>
    /// HTTP header carrying the HMAC-SHA-256 signature sent by the edge provider.
    /// Default: <c>"X-Webhook-Signature-256"</c>.
    /// Override to match your provider (e.g. <c>"CF-Webhook-Signature"</c>).
    /// </summary>
    public string CertificateWebhookSignatureHeader { get; set; } = "X-Webhook-Signature-256";
}
