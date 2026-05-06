using Granit.Taxonomy.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Taxonomy.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including <c>Granit.Taxonomy</c> entity
/// configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class TaxonomyModelBuilderExtensions
{
    /// <summary>Applies all entity configurations for the <c>Granit.Taxonomy</c> module.</summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureTaxonomyModule(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new TagConfiguration());
        modelBuilder.ApplyConfiguration(new TagAssignmentConfiguration());
        return modelBuilder;
    }
}
