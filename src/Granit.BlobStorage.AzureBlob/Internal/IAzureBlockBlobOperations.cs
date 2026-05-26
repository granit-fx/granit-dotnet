namespace Granit.BlobStorage.AzureBlob.Internal;

/// <summary>
/// Thin testing seam over the two Azure block-blob calls that
/// <see cref="AzureBlockBlobMultipartWriteStream"/> needs:
/// <see cref="StageBlockAsync"/> + <see cref="CommitBlockListAsync"/>. Lets the
/// stream stay decoupled from <c>Azure.Storage.Blobs.Specialized.BlockBlobClient</c>
/// (which has no Granit-style interface) so unit tests can mock with NSubstitute
/// instead of standing up a transport mock.
/// </summary>
/// <remarks>
/// <para>
/// <b>Block IDs.</b> Azure requires all block IDs on a given commit to share the
/// same byte length (the SDK rejects mixed-length IDs at <c>CommitBlockList</c>
/// time). Callers MUST therefore generate IDs from a fixed-length source — a
/// 16-byte Guid base64-encoded yields 24 chars, well under the 64-byte limit and
/// trivially uniform.
/// </para>
/// <para>
/// <b>No explicit abort.</b> Azure block blobs have no equivalent to S3's
/// <c>AbortMultipartUpload</c>: staged-but-uncommitted blocks expire after 7 days
/// per the service contract. Abort paths therefore simply skip
/// <see cref="CommitBlockListAsync"/> — there are no resources to clean up
/// synchronously.
/// </para>
/// </remarks>
internal interface IAzureBlockBlobOperations
{
    /// <summary>Stages a single block by id; the bytes become committed only once
    /// <see cref="CommitBlockListAsync"/> includes the same id in its list.</summary>
    Task StageBlockAsync(string blockId, Stream content, CancellationToken cancellationToken);

    /// <summary>Commits the given block ids — in order — as the final block-blob content.</summary>
    Task CommitBlockListAsync(IReadOnlyList<string> blockIds, string contentType, CancellationToken cancellationToken);
}
