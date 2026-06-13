namespace Granit.Imaging.AI.Internal;

/// <summary>
/// Default <see cref="IAIImageSource"/> that resolves no images. Applications register their own
/// implementation (over their blob/attachment store) to enable image-reading tools.
/// </summary>
internal sealed class NullAIImageSource : IAIImageSource
{
    public Task<AIImageData?> GetImageAsync(string reference, CancellationToken cancellationToken = default) =>
        Task.FromResult<AIImageData?>(null);
}
