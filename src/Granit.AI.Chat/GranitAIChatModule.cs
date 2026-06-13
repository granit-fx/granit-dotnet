using Granit.Modularity;

namespace Granit.AI.Chat;

/// <summary>
/// Granit module for conversational AI (ADR-067): the <see cref="Domain.Conversation"/> /
/// <see cref="Domain.Message"/> aggregates and the <see cref="IConversationStore"/> abstraction.
/// Persistence is provided by <c>Granit.AI.Chat.EntityFrameworkCore</c> and the HTTP surface by
/// <c>Granit.AI.Chat.Endpoints</c>.
/// </summary>
public sealed class GranitAIChatModule : GranitModule;
