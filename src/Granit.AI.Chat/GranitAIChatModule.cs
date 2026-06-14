using Granit.AI.Chat.Internal;
using Granit.AI.Tools;
using Granit.Guids;
using Granit.Modularity;
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
    typeof(GranitGuidsModule))]
public sealed class GranitAIChatModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.TryAddScoped<IChatService, ChatService>();
}
