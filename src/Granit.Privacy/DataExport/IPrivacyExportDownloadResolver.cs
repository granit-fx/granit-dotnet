namespace Granit.Privacy.DataExport;

/// <summary>
/// Resolves the bytes of a completed privacy export — both the manifest sidecar
/// and the individual shard archives — for download endpoints. Abstracts the
/// storage tier so endpoints stay in <c>Granit.Privacy.Endpoints</c> without
/// depending on <c>IBlobStoreProvider</c> internals.
/// </summary>
/// <remarks>
/// <para>
/// Shards aren't registered in the <c>IBlobStorage</c> descriptor table — they're
/// written directly via <c>IBlobStoreProvider.OpenWriteMultipartAsync</c> from
/// the assembly job, with object keys derived from the request ID. The
/// resolver reads the manifest sidecar first to learn the shard count and to
/// validate the requested <c>shardIndex</c>, then streams the raw bytes back to
/// the caller.
/// </para>
/// <para>
/// Streaming through the BFF (rather than 302-redirecting to a presigned URL)
/// keeps every download under the step-up auth gate and the ROPA audit trail.
/// Hosts that need direct cloud-to-client transfers can replace this resolver.
/// </para>
/// </remarks>
public interface IPrivacyExportDownloadResolver
{
    /// <summary>Returns the manifest's shard count plus the manifest blob reference.</summary>
    Task<PrivacyExportManifestSummary> ReadManifestSummaryAsync(
        Guid requestId,
        CancellationToken cancellationToken);

    /// <summary>Opens a read stream over the manifest JSON sidecar.</summary>
    Task<PrivacyExportDownloadPayload> OpenManifestAsync(
        Guid requestId,
        CancellationToken cancellationToken);

    /// <summary>Opens a read stream over the specified shard ZIP.</summary>
    /// <exception cref="ArgumentOutOfRangeException">Shard index is out of bounds for the request's manifest.</exception>
    Task<PrivacyExportDownloadPayload> OpenShardAsync(
        Guid requestId,
        int shardIndex,
        CancellationToken cancellationToken);
}

/// <summary>
/// Snapshot of the manifest sidecar's downloadable surface — used by endpoints
/// to validate shard indexes and to decide compat redirects.
/// </summary>
/// <param name="ShardCount">Number of shard archives the assembly produced.</param>
/// <param name="ManifestObjectKey">Manifest sidecar object key inside the export bucket.</param>
public sealed record PrivacyExportManifestSummary(int ShardCount, string ManifestObjectKey);

/// <summary>
/// Read-only stream of either the manifest JSON or a single shard archive,
/// plus the metadata required to serve it as an HTTP file response.
/// </summary>
/// <param name="Content">The byte stream — the caller must dispose it.</param>
/// <param name="ContentType">MIME type to set on the response (e.g. <c>application/zip</c>).</param>
/// <param name="FileName">Suggested download filename, used in the <c>Content-Disposition</c> header.</param>
/// <param name="LengthBytes">Total stream length when known, or <c>null</c> for streaming.</param>
public sealed record PrivacyExportDownloadPayload(
    Stream Content,
    string ContentType,
    string FileName,
    long? LengthBytes);
