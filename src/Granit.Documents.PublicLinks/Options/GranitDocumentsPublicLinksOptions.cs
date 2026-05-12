using System.ComponentModel.DataAnnotations;

namespace Granit.Documents.PublicLinks.Options;

/// <summary>Configuration options for <c>Granit.Documents.PublicLinks</c>.</summary>
public sealed class GranitDocumentsPublicLinksOptions
{
    /// <summary>Configuration section key (<c>"Granit:Documents:PublicLinks"</c>).</summary>
    public const string SectionName = "Granit:Documents:PublicLinks";

    /// <summary>
    /// Default TTL applied when the caller does not request one explicitly.
    /// Defaults to 7 days. Values exceeding <see cref="MaxTtl"/> are clamped down.
    /// </summary>
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Hard ceiling on TTL — protects against operators issuing effectively
    /// permanent share links. Defaults to 90 days. Must be ≥ <see cref="DefaultTtl"/>.
    /// </summary>
    public TimeSpan MaxTtl { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    /// HMAC pepper used to hash bearer tokens at rest. Sourced from Vault via
    /// <c>Granit.Configuration.Vault</c>. The empty default is intentional: hosts
    /// that fail to configure the key are rejected at startup by the
    /// <see cref="ValidateAttribute"/>-style validator. Minimum length is 32 bytes
    /// (HMAC-SHA256 block size) to reject anaemic peppers at startup.
    /// </summary>
    [MinLength(32)]
    public byte[] SigningKey { get; set; } = [];

    /// <summary>
    /// Per-IP rate-limit ceiling, expressed in successful redemptions per minute.
    /// Enforced by the endpoints package (F18.3).
    /// </summary>
    [Range(1, 10_000)]
    public int RateLimitPerMinute { get; set; } = 60;

    /// <summary>
    /// Default <c>MaxUses</c> applied when the caller does not specify one.
    /// <c>null</c> means unlimited within the TTL window.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int? DefaultMaxUses { get; set; }

    /// <summary>
    /// Pruning configuration for the daily expired-link cleanup job
    /// (<c>Granit.Documents.PublicLinks.BackgroundJobs</c>).
    /// </summary>
    public PruningOptions Pruning { get; set; } = new();
}

/// <summary>
/// Pruning configuration for the expired public-link cleanup job.
/// </summary>
public sealed class PruningOptions
{
    /// <summary>
    /// Whether the recurring prune job is allowed to delete rows. Defaults to <c>true</c>.
    /// Hosts that own their retention strategy can disable the job entirely.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Grace period kept after a link is revoked or has expired before the row is
    /// physically deleted. Default 90 days — preserves audit evidence for incident
    /// response while keeping the table from growing unbounded.
    /// </summary>
    public TimeSpan RetentionAfterRevocation { get; set; } = TimeSpan.FromDays(90);
}
