using Granit.Documents.EntityFrameworkCore.Internal;
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
    public static ModelBuilder ConfigureDocumentsModule(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new FolderConfiguration());
        modelBuilder.ApplyConfiguration(new DocumentConfiguration());
        modelBuilder.ApplyConfiguration(new DocumentVersionConfiguration());
        modelBuilder.ApplyConfiguration(new DocumentShareConfiguration());
        return modelBuilder;
    }
}
