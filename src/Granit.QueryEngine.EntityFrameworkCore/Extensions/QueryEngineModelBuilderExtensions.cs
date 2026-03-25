using Granit.QueryEngine.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.QueryEngine.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit QueryEngine entity
/// configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class QueryEngineModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit QueryEngine module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureQueryEngineModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new SavedViewEntityConfiguration());
        return modelBuilder;
    }
}
