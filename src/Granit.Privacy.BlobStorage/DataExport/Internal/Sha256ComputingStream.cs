using System.Security.Cryptography;

namespace Granit.Privacy.BlobStorage.DataExport.Internal;

/// <summary>
/// Forward-only write-through SHA-256 digest. Mirrors
/// <see cref="WriteCountingStream"/>: every byte handed to the wrapped stream is
/// also fed into an <see cref="IncrementalHash"/>, so the shard's content
/// digest is available at close time without a second pass over the bytes.
/// </summary>
/// <remarks>
/// The shard pipeline layers the streams as
/// <c>ZipArchive → Sha256ComputingStream → WriteCountingStream → MultipartWriteStream</c>,
/// so the sha256 covers exactly the bytes that land in blob storage (post-ZIP
/// framing including the central directory). The digest is finalised by
/// <see cref="ComputeHashAndReset"/> just before the multipart upload is
/// committed.
/// </remarks>
internal sealed class Sha256ComputingStream(Stream inner, bool leaveOpen)
    : WriteOnlyStreamDecorator(inner, leaveOpen)
{
    private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

    protected override void OnWrite(ReadOnlySpan<byte> data) => _hash.AppendData(data);

    /// <summary>
    /// Finalises the running digest and returns its 32-byte SHA-256 result. Resets
    /// the hash so the same stream cannot be re-used for a second digest — call
    /// exactly once per shard, immediately before the multipart upload commits.
    /// </summary>
    public byte[] ComputeHashAndReset() => _hash.GetHashAndReset();

    protected override void DisposeCore() => _hash.Dispose();
}
