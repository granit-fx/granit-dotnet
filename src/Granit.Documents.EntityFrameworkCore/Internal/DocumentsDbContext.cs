using Granit.DataFiltering;
using Granit.Documents.Domain;
using Granit.Documents.EntityFrameworkCore.Extensions;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Documents.EntityFrameworkCore.Internal;

/// <summary>
/// Dedicated EF Core DbContext for Granit.Documents.
/// </summary>
/// <remarks>
/// <para>
/// Isolated from the host application's DbContext to avoid coupling. Aggregate roots are
/// added story-by-story along with their <c>IEntityTypeConfiguration</c> classes wired
/// by <see cref="DocumentsModelBuilderExtensions.ConfigureDocumentsModule"/>.
/// </para>
/// <para>
/// Compatible with SQL Server and PostgreSQL.
/// </para>
/// </remarks>
internal sealed class DocumentsDbContext(
    DbContextOptions<DocumentsDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    /// <summary>Tenant-scoped folder hierarchy (one tenant-root row per tenant).</summary>
    public DbSet<Folder> Folders { get; set; } = null!;

    /// <summary>Tenant-scoped documents — every row points at a folder via <see cref="Document.FolderId"/>.</summary>
    public DbSet<Document> Documents { get; set; } = null!;

    /// <summary>Append-only version history. The current pointer lives on <see cref="Document.CurrentVersionId"/>.</summary>
    public DbSet<DocumentVersion> DocumentVersions { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureDocumentsModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
