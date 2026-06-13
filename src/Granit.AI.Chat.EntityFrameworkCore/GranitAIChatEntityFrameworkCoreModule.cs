using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.AI.Chat.EntityFrameworkCore;

/// <summary>
/// Granit module registering EF Core persistence for AI Chat. The host configures the
/// <see cref="Internal.AIChatDbContext"/> (connection string); this module registers the
/// owner-scoped <see cref="IConversationStore"/>.
/// </summary>
[DependsOn(
    typeof(GranitAIChatModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitAIChatEntityFrameworkCoreModule : GranitModule;
