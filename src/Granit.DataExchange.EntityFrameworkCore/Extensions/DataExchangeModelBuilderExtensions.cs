using Granit.DataExchange.EntityFrameworkCore.Internal.Export.Configurations;
using Granit.DataExchange.EntityFrameworkCore.Internal.Import.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.DataExchange.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit data exchange entity
/// configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class DataExchangeModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit DataExchange module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureDataExchangeModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ImportJobConfiguration());
        modelBuilder.ApplyConfiguration(new SavedMappingEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ExternalIdMappingEntityConfiguration());
        modelBuilder.ApplyConfiguration(new ExportJobConfiguration());
        modelBuilder.ApplyConfiguration(new ExportPresetEntityConfiguration());
        return modelBuilder;
    }
}
