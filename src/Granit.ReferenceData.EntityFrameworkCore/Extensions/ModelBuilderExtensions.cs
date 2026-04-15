using Granit.ReferenceData.Domain;
using Granit.ReferenceData.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.ReferenceData.EntityFrameworkCore.Extensions;

/// <summary>
/// EF Core <see cref="ModelBuilder"/> extensions for reference data entity configuration.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies a <see cref="ReferenceDataEntityTypeConfiguration{TEntity}"/> to the model.
    /// </summary>
    /// <typeparam name="TEntity">The concrete reference data entity type.</typeparam>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="configuration">The entity type configuration instance.</param>
    /// <returns>The model builder for chaining.</returns>
    public static ModelBuilder ConfigureReferenceData<TEntity>(
        this ModelBuilder modelBuilder,
        ReferenceDataEntityTypeConfiguration<TEntity> configuration)
        where TEntity : ReferenceDataEntity
    {
        modelBuilder.ApplyConfiguration(configuration);
        return modelBuilder;
    }
}
