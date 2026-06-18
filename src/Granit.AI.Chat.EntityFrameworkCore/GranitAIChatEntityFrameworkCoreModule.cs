using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Granit.QueryEngine.EntityFrameworkCore;

namespace Granit.AI.Chat.EntityFrameworkCore;

/// <summary>
/// Granit module registering EF Core persistence for AI Chat. The host configures the
/// <see cref="Internal.AIChatDbContext"/> (connection string); this module registers the
/// owner-scoped <see cref="IConversationStore"/> and the <c>IQueryEngine&lt;Message&gt;</c> runtime
/// backing the backwards-paginated messages endpoint.
/// </summary>
[DependsOn(
    typeof(GranitAIChatModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule),
    typeof(GranitQueryEngineEntityFrameworkCoreModule))]
public sealed class GranitAIChatEntityFrameworkCoreModule : GranitModule;
