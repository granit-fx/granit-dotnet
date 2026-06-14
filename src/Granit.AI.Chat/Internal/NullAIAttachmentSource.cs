using Granit.AI.Chat.Attachments;

namespace Granit.AI.Chat.Internal;

/// <summary>
/// Default <see cref="IAIAttachmentSource"/> that resolves no attachments. Applications register
/// their own implementation (over their transient blob/attachment store) to enable attachments.
/// </summary>
internal sealed class NullAIAttachmentSource : IAIAttachmentSource
{
    public Task<AIAttachmentData?> GetAsync(string reference, CancellationToken cancellationToken = default) =>
        Task.FromResult<AIAttachmentData?>(null);
}
