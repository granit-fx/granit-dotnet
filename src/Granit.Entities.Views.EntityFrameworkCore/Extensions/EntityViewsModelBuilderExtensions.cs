using Granit.Entities.Views.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Entities.Views.EntityFrameworkCore.Extensions;

/// <summary>
/// Extensions to opt the EntityView aggregate into a <see cref="ModelBuilder"/>.
/// </summary>
public static class EntityViewsModelBuilderExtensions
{
    /// <summary>
    /// Applies the <see cref="EntityViewConfiguration"/> to the model. Call from
    /// <c>OnModelCreating</c> alongside <c>ApplyGranitConventions</c>.
    /// </summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    /// <returns>The model builder for chaining.</returns>
    public static ModelBuilder ConfigureEntityViewsModule(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfiguration(new EntityViewConfiguration());
        return modelBuilder;
    }
}
