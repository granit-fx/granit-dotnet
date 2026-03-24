using Granit.Modularity;
using Granit.Persistence;

namespace Granit.AI.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of AI workspaces, usage records, and audit entries.
/// </summary>
/// <remarks>
/// Not dead code — discovered at runtime via <c>[DependsOn]</c> reflection by the Granit module system.
/// Registers <c>AIDbContext</c>, <c>EfAIWorkspaceStore</c>, and <c>EfAIUsageStore</c>.
/// Replaces the null implementations from <c>Granit.AI</c> with EF Core-backed persistence.
/// </remarks>
[DependsOn(
    typeof(GranitAIModule),
    typeof(GranitPersistenceModule))]
public sealed class GranitAIEntityFrameworkCoreModule : GranitModule;
