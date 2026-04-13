using Granit.Persistence.EntityFrameworkCore.Migrations.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including the Expand &amp; Contract
/// migration progress tracking table in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class MigrationsModelBuilderExtensions
{
    /// <summary>
    /// Applies entity configurations for the Granit Expand &amp; Contract migration tracking module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureMigrationsModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new MigrationProgressConfiguration());
        return modelBuilder;
    }
}
