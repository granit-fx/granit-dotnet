using Granit.AI.Chat.BlobStorage.Extensions;
using Granit.BlobStorage;
using Granit.Modularity;

namespace Granit.AI.Chat.BlobStorage;

/// <summary>
/// Wires <c>Granit.BlobStorage</c> as the <c>IAIAttachmentSource</c> for <c>Granit.AI.Chat</c>.
/// Attachment references are resolved as validated blob bytes via <see cref="IBlobContentReader"/>,
/// then injected as untrusted document context in the AI turn (ADR-067).
/// </summary>
[DependsOn(typeof(GranitAIChatModule))]
[DependsOn(typeof(GranitBlobStorageModule))]
public sealed class GranitAIChatBlobStorageModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddBlobStorageChatAttachments();
}
