namespace Granit.Imaging.AI;

/// <summary>Image bytes plus their MIME type.</summary>
public sealed record AIImageData(ReadOnlyMemory<byte> Bytes, string ContentType);

/// <summary>
/// Application-provided seam that resolves a model-supplied image reference (an attachment id,
/// blob key, …) to its bytes for the <c>extract_text_from_image</c> tool. The framework does not
/// know how an application stores images, so the default <see cref="Internal.NullAIImageSource"/>
/// resolves nothing — register an implementation to enable the tool against your store.
/// </summary>
public interface IAIImageSource
{
    /// <summary>
    /// Returns the image for <paramref name="reference"/>, or <see langword="null"/> when it cannot
    /// be resolved or the caller is not allowed to access it.
    /// </summary>
    Task<AIImageData?> GetImageAsync(string reference, CancellationToken cancellationToken = default);
}
