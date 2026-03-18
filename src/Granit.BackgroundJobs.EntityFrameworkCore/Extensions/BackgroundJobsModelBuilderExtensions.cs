using Granit.BackgroundJobs.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.BackgroundJobs.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit background job entity
/// configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
/// <remarks>
/// Call <see cref="ConfigureBackgroundJobsModule"/> inside the host's
/// <c>OnModelCreating</c> so that EF Core migrations include the background job tables.
/// Optionally set <see cref="GranitBackgroundJobsDbProperties.DbTablePrefix"/> and
/// <see cref="GranitBackgroundJobsDbProperties.DbSchema"/> before calling this method
/// to customise table naming.
/// </remarks>
public static class BackgroundJobsModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit Background Jobs module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureBackgroundJobsModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new BackgroundJobDefinitionConfiguration());
        return modelBuilder;
    }
}
