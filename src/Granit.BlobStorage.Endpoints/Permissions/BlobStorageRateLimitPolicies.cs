namespace Granit.BlobStorage.Endpoints.Permissions;

/// <summary>
/// Rate limiting policy names for blob storage endpoints.
/// Configure limits in <c>RateLimiting:Policies</c> using these names as keys.
/// </summary>
/// <example>
/// <code>
/// "RateLimiting": {
///   "Policies": {
///     "blob-upload":   { "Algorithm": "TokenBucket", "TokenLimit": 20, "TokensPerPeriod": 5, "ReplenishmentPeriod": "00:00:10" },
///     "blob-download": { "PermitLimit": 100, "Window": "00:01:00" },
///     "blob-admin":    { "PermitLimit": 10,  "Window": "00:01:00" }
///   }
/// }
/// </code>
/// </example>
public static class BlobStorageRateLimitPolicies
{
    /// <summary>
    /// Policy for upload initiation and confirmation (pre-signed URL generation + validation pipeline).
    /// Recommended: TokenBucket with burst allowance.
    /// </summary>
    public const string Upload = "blob-upload";

    /// <summary>
    /// Policy for download URL generation.
    /// Recommended: SlidingWindow, 100 requests/minute.
    /// </summary>
    public const string Download = "blob-download";

    /// <summary>
    /// Policy for administrative operations (orphan cleanup, bulk delete).
    /// Recommended: FixedWindow, 10 requests/minute.
    /// </summary>
    public const string Admin = "blob-admin";
}
