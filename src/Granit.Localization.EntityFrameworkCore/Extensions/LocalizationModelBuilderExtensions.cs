using Granit.Localization.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Localization.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit localization entity
/// configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class LocalizationModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit Localization module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureLocalizationModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new LocalizationOverrideConfiguration());
        return modelBuilder;
    }
}
