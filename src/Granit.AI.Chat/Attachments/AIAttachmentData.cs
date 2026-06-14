namespace Granit.AI.Chat.Attachments;

/// <summary>
/// The bytes of an attachment, resolved by an <see cref="IAIAttachmentSource"/> from an
/// <see cref="AIAttachment.Reference"/>. The framework runs text extraction over these bytes
/// and never stores them — storage is the application's concern.
/// </summary>
/// <param name="Bytes">The raw file content.</param>
/// <param name="ContentType">The authoritative MIME type, used to dispatch text extraction.</param>
/// <param name="FileName">The original file name, surfaced to the agent as a label.</param>
public sealed record AIAttachmentData(ReadOnlyMemory<byte> Bytes, string ContentType, string FileName);
