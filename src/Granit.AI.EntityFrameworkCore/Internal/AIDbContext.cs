using Granit.AI.EntityFrameworkCore.Extensions;
using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.AI.EntityFrameworkCore.Internal;

/// <summary>
/// Dedicated EF Core DbContext for Granit AI persistence.
/// </summary>
/// <remarks>
/// Isolated from the host application's DbContext. Stores workspace configurations,
/// usage records, and audit entries. Compatible with SQL Server and PostgreSQL.
/// </remarks>
internal sealed class AIDbContext(
    DbContextOptions<AIDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    /// <summary>Dynamic AI workspace configurations.</summary>
    public DbSet<AIWorkspaceEntity> Workspaces { get; set; } = null!;

    /// <summary>AI usage tracking records (token counts, costs).</summary>
    public DbSet<AIUsageRecordEntity> UsageRecords { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureAIModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
