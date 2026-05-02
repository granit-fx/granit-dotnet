using Microsoft.EntityFrameworkCore;

namespace Granit.Documents.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including <c>Granit.Documents</c> entity
/// configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class DocumentsModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the <c>Granit.Documents</c> module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    /// <remarks>
    /// Phase-1 scaffolding: no entity configurations registered yet. Subsequent stories
    /// (F2 <c>Folder</c>, F3 <c>Document</c>, …) will add their <c>IEntityTypeConfiguration</c>
    /// classes here.
    /// </remarks>
    public static ModelBuilder ConfigureDocumentsModule(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        return modelBuilder;
    }
}
