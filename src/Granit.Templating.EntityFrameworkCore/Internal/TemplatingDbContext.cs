using Granit.Core.DataFiltering;
using Granit.Core.MultiTenancy;
using Granit.Persistence.Extensions;
using Granit.Templating.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Templating.EntityFrameworkCore.Internal;

/// <summary>
/// Dedicated EF Core DbContext for Granit template revisions.
/// </summary>
/// <remarks>
/// Isolated from the host application's DbContext to avoid coupling.
/// Compatible with PostgreSQL and SQL Server.
/// </remarks>
internal sealed class TemplatingDbContext(
    DbContextOptions<TemplatingDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    /// <summary>All template revisions (Draft, PendingReview, Published, Archived).</summary>
    public DbSet<TemplateRevisionEntity> TemplateRevisions { get; set; } = null!;

    /// <summary>Template categories for organizing templates by domain.</summary>
    public DbSet<TemplateCategoryEntity> TemplateCategories { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureTemplatingModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
