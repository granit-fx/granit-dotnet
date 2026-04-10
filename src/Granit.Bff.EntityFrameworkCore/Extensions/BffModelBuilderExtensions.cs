using Granit.Bff.EntityFrameworkCore.Internal.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.Bff.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit BFF entity
/// configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
/// <remarks>
/// Call <see cref="ConfigureBffModule"/> inside the host's
/// <c>OnModelCreating</c> so that EF Core migrations include the BFF session tables.
/// Optionally set <see cref="GranitBffDbProperties.DbTablePrefix"/> and
/// <see cref="GranitBffDbProperties.DbSchema"/> before calling this method
/// to customise table naming.
/// </remarks>
public static class BffModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit BFF module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureBffModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new BffSessionEntityConfiguration());
        return modelBuilder;
    }
}
