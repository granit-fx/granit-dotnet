using Granit.AI.Chat.Extensions;
using Granit.AI.Chat.Internal;
using Granit.AI.Chat.Settings;
using Granit.AI.Prompts;
using Granit.AI.Tools;
using Granit.Guids;
using Granit.Modularity;
using Granit.Settings;
using Granit.TextExtraction;
using Microsoft.Extensions.DependencyInjection;
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
    typeof(GranitAIPromptsModule),
    typeof(GranitAIToolsModule),
    typeof(GranitGuidsModule),
    typeof(GranitSettingsModule),
    typeof(GranitTextExtractionModule))]
public sealed class GranitAIChatModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.TryAddScoped<IChatService, ChatService>();

        // Badge resolution expands the turn's '/' prompt-catalogue references into the instruction
        // (owner-scoped via IPromptTemplateStore). Always present; resolves to nothing when no refs.
        context.Services.TryAddScoped<IPromptBadgeResolver, PromptBadgeResolver>();

        // The mention registry and context resolver are always present so a turn can carry
        // mentions even before the application opts any resolver in (they then resolve to nothing).
        AIChatMentionsServiceCollectionExtensions.AddCoreServices(context.Services);

        // Likewise the attachment text resolver — with the Null source it resolves nothing until
        // the application registers its own IAIAttachmentSource over its transient blob store.
        AIChatAttachmentsServiceCollectionExtensions.AddCoreServices(context.Services);

        // Per-user setting definitions in the Granit AI Chat namespace are auto-discovered by
        // GranitSettingsModule. Only the chat-capable workspace catalog for the settings UI is
        // registered here.
        context.Services.TryAddScoped<IChatWorkspaceCatalog, ChatWorkspaceCatalog>();

        // The suggestion resolver is always present so a turn can carry suggested actions even
        // before any module contributes a provider (it then resolves to none).
        AIChatSuggestionsServiceCollectionExtensions.AddCoreServices(context.Services);

        // The clarification tool is core chat behaviour — always available to the agent so it can
        // ask clickable disambiguating questions (it halts the loop via the interrupt primitive).
        context.Services.AddScoped<IAITool, RequestClarificationTool>();
    }
}
