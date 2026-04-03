using Granit.Scheduling.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Scheduling.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit scheduling entity
/// configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
/// <remarks>
/// Call <see cref="ConfigureSchedulingModule"/> inside the host's
/// <c>OnModelCreating</c> so that EF Core migrations include the scheduling tables.
/// Optionally set <see cref="GranitSchedulingDbProperties.DbTablePrefix"/> and
/// <see cref="GranitSchedulingDbProperties.DbSchema"/> before calling this method
/// to customise table naming.
/// </remarks>
public static class SchedulingModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit Scheduling module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureSchedulingModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ScheduledActionConfiguration());
        return modelBuilder;
    }
}
