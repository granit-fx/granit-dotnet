using Granit.AI.Chat.Attachments;
using Granit.BlobStorage;

namespace Granit.AI.Chat.BlobStorage;

internal sealed class BlobStorageAIAttachmentSource(IBlobContentReader contentReader) : IAIAttachmentSource
{
    public async Task<AIAttachmentData?> GetAsync(string reference, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(reference, out Guid blobId))
        {
            return null;
        }

        BlobContent? content = await contentReader.ReadAsync(blobId, cancellationToken).ConfigureAwait(false);
        if (content is null)
        {
            return null;
        }

        return new AIAttachmentData(content.Bytes, content.ContentType, content.FileName);
    }
}
