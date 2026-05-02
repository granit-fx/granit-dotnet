using Granit.DataFiltering;
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
/// Isolated from the host application's DbContext to avoid coupling. Phase-1 scaffolding:
/// no <see cref="DbSet{TEntity}"/> declarations yet — aggregate roots arrive in subsequent
/// stories (F2 <c>Folder</c>, F3 <c>Document</c>, …) along with their <c>IEntityTypeConfiguration</c>
/// classes wired by <see cref="DocumentsModelBuilderExtensions.ConfigureDocumentsModule"/>.
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
    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureDocumentsModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
