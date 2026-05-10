namespace Granit.Documents.Renditions;

/// <summary>
/// Output of a rendition pipeline run. The bytes are returned in-memory; the storage
/// layer is responsible for persisting them through <c>Granit.BlobStorage</c>.
/// </summary>
/// <param name="Content">Raw bytes of the rendition.</param>
/// <param name="ContentType">MIME type of <see cref="Content"/>.</param>
/// <param name="Width">Output width in pixels (or <c>null</c> for non-raster formats).</param>
/// <param name="Height">Output height in pixels.</param>
public sealed record RenditionResult(
    byte[] Content,
    string ContentType,
    int? Width = null,
    int? Height = null);
