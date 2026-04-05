using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Templating.EntityFrameworkCore.Entities;
using Granit.Templating.EntityFrameworkCore.Extensions;
using Granit.Workflow.Domain;
using Granit.Workflow.EntityFrameworkCore;
using Granit.Workflow.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Templating.EntityFrameworkCore.Internal;

/// <summary>
/// Dedicated EF Core DbContext for Granit template revisions.
/// </summary>
internal sealed class TemplatingDbContext(
    DbContextOptions<TemplatingDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options), IWorkflowDbContext
{
    public DbSet<TemplateRevisionEntity> TemplateRevisions { get; set; } = null!;
    public DbSet<TemplateCategoryEntity> TemplateCategories { get; set; } = null!;
    public DbSet<WorkflowTransitionRecord> WorkflowTransitionRecords => Set<WorkflowTransitionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureWorkflowModule();
        modelBuilder.ConfigureTemplatingModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
