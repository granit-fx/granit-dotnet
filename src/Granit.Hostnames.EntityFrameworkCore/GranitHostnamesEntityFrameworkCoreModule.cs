using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Workflow.EntityFrameworkCore;

namespace Granit.Hostnames.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of managed hostnames.
/// Registers <c>HostnamesDbContext</c>, <c>EfManagedHostnameStore</c>, <c>EfHostnameResolver</c>,
/// and the <c>WorkflowTransitionInterceptor</c> for the hostname lifecycle audit trail.
/// </summary>
/// <remarks>
/// Register via the host application's builder:
/// <code>
/// builder.AddGranitHostnamesEntityFrameworkCore(opt =>
///     opt.UseNpgsql(connectionString));
/// </code>
/// </remarks>
[DependsOn(
    typeof(GranitHostnamesModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule),
    typeof(GranitWorkflowEntityFrameworkCoreModule))]
public sealed class GranitHostnamesEntityFrameworkCoreModule : GranitModule;
