using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Workflow.EntityFrameworkCore;

namespace Granit.Templating.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of template revisions.
/// </summary>
[DependsOn(
    typeof(GranitPersistenceEntityFrameworkCoreModule),
    typeof(GranitTemplatingModule),
    typeof(GranitWorkflowEntityFrameworkCoreModule))]
public sealed class GranitTemplatingEntityFrameworkCoreModule : GranitModule;
