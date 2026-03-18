using Granit.Querying.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Querying.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit querying entity
/// configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class QueryingModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit Querying module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureQueryingModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new SavedViewEntityConfiguration());
        return modelBuilder;
    }
}
