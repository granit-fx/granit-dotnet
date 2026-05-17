namespace Granit.BlobStorage.Proxy.Internal;

/// <summary>
/// Metadata stored alongside an ephemeral proxy token.
/// Serialized to JSON and persisted in <see cref="IBlobProxyTokenStore"/>.
/// </summary>
internal sealed record ProxyTokenEntry(
    ProxyTokenType Type,
    string Bucket,
    string ObjectKey,
    Guid BlobId,
    Guid? TenantId,
    string? ContentType,
    long? MaxBytes,
    string? DownloadFileName);
