using Granit.Activities.EntityFrameworkCore.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.Activities.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit activity entity
/// configurations in a host-owned <see cref="DbContext"/> (for hosts that prefer
/// a single DbContext to the isolated <see cref="Internal.ActivitiesDbContext"/>).
/// </summary>
public static class ActivitiesModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit Activities module.
    /// </summary>
    public static ModelBuilder ConfigureActivitiesModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new ActivityConfiguration());
        return modelBuilder;
    }
}
