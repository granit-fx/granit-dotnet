using Granit.AI.Chat.Diagnostics;
using Granit.AI.Chat.Domain;
using Granit.AI.Chat.Extensions;
using Granit.AI.Chat.Internal;
using Granit.AI.Chat.Mentions;
using Granit.AI.Chat.Queries;
using Granit.AI.Chat.Settings;
using Granit.AI.Prompts;
using Granit.AI.Tools;
using Granit.Diagnostics;
using Granit.Guids;
using Granit.Mentions;
using Granit.Modularity;
using Granit.QueryEngine;
using Granit.QueryEngine.Extensions;
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
    typeof(GranitMentionsModule),
    typeof(GranitQueryEngineModule),
    typeof(GranitSettingsModule),
    typeof(GranitTextExtractionModule))]
public sealed class GranitAIChatModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        GranitActivitySourceRegistry.Register(AIChatActivitySource.Name);
        context.Services.TryAddSingleton<AIChatMetrics>();

        context.Services.TryAddScoped<IChatService, ChatService>();

        // Keyset (cursor) query definition backing the backwards-paginated messages endpoint.
        // The endpoint drives IQueryEngine<Message> directly over an owner-scoped source.
        context.Services.AddQueryDefinition<Message, ChatMessageQueryDefinition>();

        // Badge resolution expands the turn's '/' prompt-catalogue references into the instruction
        // (owner-scoped via IPromptTemplateStore). Always present; resolves to nothing when no refs.
        context.Services.TryAddScoped<IPromptBadgeResolver, PromptBadgeResolver>();

        // The mention registry/authorizer and the 'mentions' picker facade come from
        // GranitMentionsModule (a dependency); the AI-specific per-turn context resolver that wraps a
        // resolved mention as untrusted prompt context is registered here.
        context.Services.TryAddScoped<IAIMentionContextResolver, AIMentionContextResolver>();

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
