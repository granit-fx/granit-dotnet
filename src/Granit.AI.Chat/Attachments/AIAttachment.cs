namespace Granit.AI.Chat.Attachments;

/// <summary>
/// A file the user attached to a turn (ADR-067). v1 follows the model-agnostic text-extraction
/// path: the opaque <see cref="Reference"/> is resolved to bytes by the application's
/// <see cref="IAIAttachmentSource"/> under the caller's ACLs, its text is extracted and injected
/// as untrusted context. The declared <see cref="ContentType"/> and <see cref="SizeBytes"/> are
/// validated against the configured limits before any work starts.
/// </summary>
/// <param name="Reference">The opaque attachment identifier, interpreted by the application source.</param>
/// <param name="FileName">The original file name, surfaced to the agent as a label.</param>
/// <param name="ContentType">The declared MIME type, matched against the allowed-type list.</param>
/// <param name="SizeBytes">The declared size in bytes, checked against the size limit.</param>
public sealed record AIAttachment(string Reference, string FileName, string ContentType, long SizeBytes);
