namespace Granit.AI.Chat.Attachments;

/// <summary>
/// Application-implemented seam that resolves an attachment <see cref="AIAttachment.Reference"/>
/// to its bytes (ADR-067). Mirrors the vision <c>IAIImageSource</c> seam: how an attachment is
/// stored (transient blob storage, conversation-scoped container, …) is an application concern,
/// decoupled from the AI text-extraction path the framework owns.
/// </summary>
/// <remarks>
/// The implementation MUST run under the caller's ACLs and return <see langword="null"/> when the
/// reference is unknown or the caller may not read it — the attachment is then dropped and nothing
/// about it enters the prompt. The default <c>NullAIAttachmentSource</c> resolves nothing until an
/// application registers its own.
/// </remarks>
public interface IAIAttachmentSource
{
    /// <summary>
    /// Resolves an attachment reference to its bytes, under the caller's ACLs.
    /// </summary>
    /// <param name="reference">The opaque attachment identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The attachment bytes, or <see langword="null"/> when absent or not accessible.</returns>
    Task<AIAttachmentData?> GetAsync(string reference, CancellationToken cancellationToken = default);
}
