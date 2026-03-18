using Granit.Features.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Features.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit feature override entity
/// configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class FeaturesModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit Features module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureFeaturesModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TenantFeatureOverrideConfiguration());
        return modelBuilder;
    }
}
