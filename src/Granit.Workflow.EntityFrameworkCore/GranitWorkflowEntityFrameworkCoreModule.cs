using Granit.Modularity;
using Granit.Persistence;
using Granit.Workflow.EntityFrameworkCore.Extensions;

namespace Granit.Workflow.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of workflow transition records.
/// Registers <see cref="Interceptors.WorkflowTransitionInterceptor"/> for automatic
/// ISO 27001-compliant audit trail creation during <c>SaveChanges</c>.
/// </summary>
/// <remarks>
/// <para>
/// The host application's DbContext must implement <see cref="IWorkflowDbContext"/>
/// and call <c>modelBuilder.ConfigureWorkflowModule()</c> in <c>OnModelCreating</c>.
/// </para>
/// <para>
/// Register via:
/// <code>
/// services.AddGranitWorkflowEntityFrameworkCore&lt;AppDbContext&gt;();
/// </code>
/// The generic overload also registers <see cref="IWorkflowHistoryQuery"/>
/// and <see cref="IWorkflowTransitionRecorder"/> implementations.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitPersistenceModule),
    typeof(GranitWorkflowModule))]
public sealed class GranitWorkflowEntityFrameworkCoreModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitWorkflowEntityFrameworkCore();
}
