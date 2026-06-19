namespace Granit.BlobStorage;

/// <summary>
/// The bytes of a validated blob, resolved by <see cref="IBlobContentReader"/>.
/// </summary>
/// <param name="Bytes">The raw file content.</param>
/// <param name="ContentType">The MIME type verified by the post-upload validation pipeline.</param>
/// <param name="FileName">The original file name as provided by the client at upload time.</param>
public sealed record BlobContent(ReadOnlyMemory<byte> Bytes, string ContentType, string FileName);
