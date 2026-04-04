namespace Granit.Timeline.Domain.ValueObjects;

/// <summary>
/// Denormalized file metadata for a timeline attachment.
/// </summary>
/// <param name="FileName">Original filename.</param>
/// <param name="ContentType">MIME content type.</param>
/// <param name="SizeBytes">File size in bytes.</param>
public sealed record FileMetadata(string FileName, string ContentType, long SizeBytes);
