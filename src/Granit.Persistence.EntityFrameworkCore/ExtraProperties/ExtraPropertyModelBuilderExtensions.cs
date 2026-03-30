using Granit.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Persistence.EntityFrameworkCore.ExtraProperties;

/// <summary>
/// EF Core <see cref="ModelBuilder"/> extensions for applying extra property mappings
/// as Shadow Properties on entity types.
/// </summary>
public static class ExtraPropertyModelBuilderExtensions
{
    /// <summary>
    /// Applies extra property mappings for <typeparamref name="TEntity"/>, adding
    /// EF Core Shadow Properties as real SQL columns for each mapped property.
    /// </summary>
    /// <typeparam name="TEntity">The entity type implementing <see cref="IHasExtraProperties"/>.</typeparam>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    /// <param name="options">The extra property mapping options declaring which properties to map.</param>
    /// <returns>The model builder for chaining.</returns>
    public static ModelBuilder ApplyExtraPropertyMappings<TEntity>(
        this ModelBuilder modelBuilder,
        ExtraPropertyMappingOptions<TEntity> options)
        where TEntity : class, IHasExtraProperties
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(options);

        if (options.Mappings.Count == 0)
        {
            return modelBuilder;
        }

        modelBuilder.Entity<TEntity>(b =>
        {
            foreach (ExtraPropertyMapping mapping in options.Mappings)
            {
                PropertyBuilder prop = b.Property(mapping.ClrType, mapping.Name);

                if (mapping.MaxLength.HasValue)
                {
                    prop.HasMaxLength(mapping.MaxLength.Value);
                }

                if (mapping.IsRequired)
                {
                    prop.IsRequired();
                }
            }
        });

        return modelBuilder;
    }
}
