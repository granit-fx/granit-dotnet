using System.Linq.Expressions;
using Granit.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Granit.Persistence.EntityFrameworkCore.Extensions;

/// <summary>
/// Maps the framework <see cref="ImageDimensions"/> value object to two flat, typed columns
/// (<c>Width</c> / <c>Height</c>) via EF Core's <c>ComplexProperty</c> — the convention-sanctioned
/// alternative to the default JSON serialization for multi-field value objects (see
/// <c>ApplyGranitConventions</c>). Use this when the dimensions must stay queryable / sortable /
/// indexable as discrete integer columns rather than living inside a JSON blob.
/// </summary>
public static class ImageDimensionsModelBuilderExtensions
{
    /// <summary>
    /// Maps an <see cref="ImageDimensions"/> property to two flat columns. The property may be
    /// optional (<c>ImageDimensions?</c>) — EF Core 10 maps a nullable complex type to nullable
    /// columns. Because the property is configured as a complex type, the value-object convention
    /// leaves it untouched (it is never an entity type).
    /// </summary>
    /// <typeparam name="TEntity">The owning entity type.</typeparam>
    /// <param name="builder">The entity type builder.</param>
    /// <param name="selector">Selects the <see cref="ImageDimensions"/> property to map.</param>
    /// <param name="widthColumnName">Column name for <see cref="ImageDimensions.Width"/>.</param>
    /// <param name="heightColumnName">Column name for <see cref="ImageDimensions.Height"/>.</param>
    /// <returns>The same <paramref name="builder"/> for chaining.</returns>
    public static EntityTypeBuilder<TEntity> MapImageDimensions<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, ImageDimensions?>> selector,
        string widthColumnName = "Width",
        string heightColumnName = "Height")
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(selector);

        builder.ComplexProperty(selector, dimensions =>
        {
            dimensions.Property(d => d.Width).HasColumnName(widthColumnName);
            dimensions.Property(d => d.Height).HasColumnName(heightColumnName);
        });

        return builder;
    }
}
