using Granit.DataFiltering;
using Granit.Hostnames.Domain;
using Granit.Hostnames.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Workflow.Domain;
using Granit.Workflow.EntityFrameworkCore;
using Granit.Workflow.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Hostnames.EntityFrameworkCore.Internal;

/// <summary>
/// Dedicated EF Core DbContext for Granit managed hostnames.
/// </summary>
/// <remarks>
/// Inherits <see cref="GranitDbContext"/> because <see cref="ManagedHostname"/> is
/// <see cref="Granit.Domain.IMultiTenant"/> — the base class parameterises the tenant filter
/// into a query parameter, preventing cross-request data leaks (ADR-061 / GranitDbContext docs).
/// Implements <see cref="IWorkflowDbContext"/> to enable the <c>WorkflowTransitionInterceptor</c>
/// audit trail for the hostname lifecycle state machine.
/// </remarks>
internal sealed class HostnamesDbContext(
    DbContextOptions<HostnamesDbContext> options,
    ICurrentTenant currentTenant,
    IDataFilter? dataFilter = null)
    : GranitDbContext(options, currentTenant, dataFilter), IWorkflowDbContext
{
    /// <summary>All registered managed hostnames.</summary>
    public DbSet<ManagedHostname> ManagedHostnames { get; set; } = null!;

    /// <inheritdoc/>
    public DbSet<WorkflowTransitionRecord> WorkflowTransitionRecords => Set<WorkflowTransitionRecord>();

    /// <inheritdoc/>
    protected override void OnGranitModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ConfigureHostnamesModule();
        modelBuilder.ConfigureWorkflowModule();
    }
}
