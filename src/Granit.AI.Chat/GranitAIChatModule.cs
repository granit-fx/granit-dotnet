using Granit.AI.Chat.Extensions;
using Granit.AI.Chat.Internal;
using Granit.AI.Tools;
using Granit.Guids;
using Granit.Modularity;
using Granit.TextExtraction;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.AI.Chat;

/// <summary>
/// Granit module for conversational AI (ADR-067): the <see cref="Domain.Conversation"/> /
/// <see cref="Domain.Message"/> aggregates, the <see cref="IConversationStore"/> abstraction, and
/// the <see cref="IChatService"/> orchestration that drives a chat turn over the
/// <c>Granit.AI.Tools</c> loop. Persistence is provided by <c>Granit.AI.Chat.EntityFrameworkCore</c>
/// and the HTTP surface by <c>Granit.AI.Chat.Endpoints</c>.
/// </summary>
[DependsOn(
    typeof(GranitAIModule),
    typeof(GranitAIToolsModule),
    typeof(GranitGuidsModule),
    typeof(GranitTextExtractionModule))]
public sealed class GranitAIChatModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddScoped<IChatService, ChatService>();

        // The mention registry and context resolver are always present so a turn can carry
        // mentions even before the application opts any resolver in (they then resolve to nothing).
        AIChatMentionsServiceCollectionExtensions.AddCoreServices(context.Services);

        // Likewise the attachment text resolver — with the Null source it resolves nothing until
        // the application registers its own IAIAttachmentSource over its transient blob store.
        AIChatAttachmentsServiceCollectionExtensions.AddCoreServices(context.Services);
    }
}
