using Granit.BlobStorage.Options;

namespace Granit.BlobStorage.S3.Options;

/// <summary>
/// Configuration for the S3-compatible blob storage provider.
/// Extends <see cref="BlobStorageOptions"/> with S3-specific connection settings.
/// </summary>
/// <remarks>
/// <para>
/// <b>S3-compatible provider</b>: set <see cref="ServiceUrl"/> to your provider's endpoint
/// (e.g. <c>https://s3.eu-west-1.amazonaws.com</c>) and <see cref="Region"/> accordingly.
/// </para>
/// <para>
/// <b>MinIO (development)</b>: set <see cref="ServiceUrl"/> to
/// <c>http://localhost:9000</c> and <see cref="ForcePathStyle"/> to <c>true</c>.
/// </para>
/// <para>
/// Credentials must be injected from <c>Granit.Vault</c> in production.
/// Never hardcode <see cref="AccessKey"/> or <see cref="SecretKey"/> in source code or
/// <c>appsettings.json</c>.
/// </para>
/// </remarks>
public sealed class S3BlobOptions : BlobStorageOptions
{
    /// <summary>
    /// S3-compatible endpoint URL.
    /// Examples: <c>https://s3.eu-west-1.amazonaws.com</c>, <c>http://localhost:9000</c>.
    /// </summary>
    /// <remarks>
    /// The scheme drives <c>AmazonS3Config.UseHttp</c>: an <c>http://</c> value emits presigned URLs
    /// in cleartext (required for MinIO dev), <c>https://</c> emits TLS presigned URLs. HTTP is only
    /// accepted for <c>localhost</c> / <c>127.0.0.1</c>; all other hosts must use HTTPS.
    /// </remarks>
    public string ServiceUrl { get; set; } = string.Empty;

    /// <summary>S3 access key. Inject from Granit.Vault; never hardcode.</summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>S3 secret key. Inject from Granit.Vault; never hardcode.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// S3 region identifier used by the SDK for request signing.
    /// Examples: <c>eu-west-1</c>, <c>us-east-1</c>. For MinIO: any non-empty value.
    /// </summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>
    /// Default S3 bucket name when using <see cref="BlobTenantIsolation.Prefix"/> strategy.
    /// </summary>
    public string DefaultBucket { get; set; } = string.Empty;

    /// <summary>
    /// Forces path-style URL addressing (<c>https://host/bucket/key</c> instead of
    /// <c>https://bucket.host/key</c>). Required for MinIO and some S3-compatible providers.
    /// Defaults to <c>true</c> for maximum compatibility.
    /// </summary>
    public bool ForcePathStyle { get; set; } = true;

    /// <summary>
    /// Multi-tenant isolation strategy. Defaults to <see cref="BlobTenantIsolation.Prefix"/>.
    /// </summary>
    public BlobTenantIsolation TenantIsolation { get; set; } = BlobTenantIsolation.Prefix;
}
