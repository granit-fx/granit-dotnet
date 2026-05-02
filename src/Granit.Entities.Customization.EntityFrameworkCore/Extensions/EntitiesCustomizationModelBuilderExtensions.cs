using Granit.Entities.Customization.EntityFrameworkCore.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.Entities.Customization.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including the entity-customization
/// table configuration in a host-owned <see cref="DbContext"/> (for hosts that
/// prefer a single DbContext over the isolated
/// <see cref="Internal.CustomizationDbContext"/>).
/// </summary>
public static class EntitiesCustomizationModelBuilderExtensions
{
    /// <summary>Applies all entity configurations for the entities-customization module.</summary>
    public static ModelBuilder ConfigureEntitiesCustomizationModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new EntityCustomizationConfiguration());
        return modelBuilder;
    }
}
