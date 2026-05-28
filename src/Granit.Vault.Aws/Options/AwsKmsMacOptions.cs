using System.ComponentModel.DataAnnotations;

namespace Granit.Vault.Aws.Options;

/// <summary>
/// Configuration for the AWS KMS-backed <see cref="ITransitMacService"/>.
/// AWS KMS does not version HMAC keys — rotation produces a new key ARN — so this
/// options class exposes a <see cref="CurrentAlias"/> and an optional
/// <see cref="PreviousAlias"/> that the operator swaps during rotation.
/// </summary>
public sealed class AwsKmsMacOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Vault:Aws:Mac";

    /// <summary>
    /// KMS alias used to sign new tags (e.g. <c>alias/granit/privacy-export-mac-current</c>).
    /// Required.
    /// </summary>
    [Required]
    public string CurrentAlias { get; set; } = string.Empty;

    /// <summary>
    /// KMS alias used as the rolling-window fallback on verify. When the operator
    /// rotates, they move this alias to point at the previous key ARN. Optional but
    /// strongly recommended — without it, every rotation invalidates in-flight tags.
    /// </summary>
    public string? PreviousAlias { get; set; }
}
