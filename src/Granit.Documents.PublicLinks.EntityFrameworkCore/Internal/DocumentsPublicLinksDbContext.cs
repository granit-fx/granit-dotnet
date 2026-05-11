using Granit.DataFiltering;
using Granit.Documents.PublicLinks.Domain;
using Granit.Documents.PublicLinks.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Documents.PublicLinks.EntityFrameworkCore.Internal;

/// <summary>
/// Dedicated EF Core DbContext for <c>Granit.Documents.PublicLinks</c>. Isolated
/// from <c>DocumentsDbContext</c> so the public-links module ships its own table
/// and can be replaced wholesale by hosts that prefer an alternate storage
/// backend.
/// </summary>
internal sealed class DocumentsPublicLinksDbContext(
    DbContextOptions<DocumentsPublicLinksDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    /// <summary>Public-link rows.</summary>
    public DbSet<DocumentPublicLink> DocumentPublicLinks { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigurePublicLinksModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
