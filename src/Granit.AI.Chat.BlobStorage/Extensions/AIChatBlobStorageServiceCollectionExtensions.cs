using Granit.AI.Chat.Extensions;
using Granit.AI.Chat.Options;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.AI.Chat.BlobStorage.Extensions;

/// <summary>
/// Registration extension for the BlobStorage-backed <see cref="Granit.AI.Chat.Attachments.IAIAttachmentSource"/>.
/// </summary>
public static class AIChatBlobStorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers <c>BlobStorageAIAttachmentSource</c> as the attachment source for Granit AI Chat.
    /// Attachment references are expected to be valid blob IDs (Guid string) from the BlobStorage
    /// upload flow (<c>POST /blobs/upload → PUT presigned → POST /blobs/{id}/confirm</c>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional override of the attachment limits.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddBlobStorageChatAttachments(
        this IServiceCollection services,
        Action<GranitAIChatAttachmentOptions>? configure = null)
        => services.AddGranitChatAttachments<BlobStorageAIAttachmentSource>(configure);
}
