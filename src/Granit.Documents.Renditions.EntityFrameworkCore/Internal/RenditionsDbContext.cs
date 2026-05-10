using Granit.DataFiltering;
using Granit.Documents.Renditions.Domain;
using Granit.Documents.Renditions.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Documents.Renditions.EntityFrameworkCore.Internal;

/// <summary>
/// Dedicated EF Core DbContext for <c>Granit.Documents.Renditions</c>. Isolated from
/// <c>DocumentsDbContext</c> so the renditions module can ship its own migrations,
/// own its quota counter mutations, and be replaced wholesale by hosts that prefer
/// an alternate storage backend.
/// </summary>
internal sealed class RenditionsDbContext(
    DbContextOptions<RenditionsDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    /// <summary>Renditions attached to a <c>DocumentVersion</c>. See F16.1 domain notes.</summary>
    public DbSet<DocumentRendition> Renditions { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureRenditionsModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
