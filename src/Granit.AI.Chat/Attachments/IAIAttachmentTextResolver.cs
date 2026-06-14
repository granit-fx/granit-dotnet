namespace Granit.AI.Chat.Attachments;

/// <summary>
/// Resolves a turn's attachments to a single context block to inject ahead of the user's message.
/// Each attachment's bytes are resolved via the <see cref="IAIAttachmentSource"/> under the
/// caller's ACLs, its text extracted (<c>Granit.TextExtraction</c>), and wrapped in the
/// untrusted-data envelope. Attachments that cannot be resolved, exceed the limits, or yield no
/// text are dropped.
/// </summary>
public interface IAIAttachmentTextResolver
{
    /// <summary>
    /// Resolves <paramref name="attachments"/> to an injectable context block.
    /// </summary>
    /// <param name="attachments">The attachments carried on the turn.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The context block to prepend to the user's message, or <see langword="null"/> when no
    /// attachment yielded usable text.
    /// </returns>
    ValueTask<string?> ResolveContextAsync(
        IReadOnlyList<AIAttachment> attachments,
        CancellationToken cancellationToken = default);
}
