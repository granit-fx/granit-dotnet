using Granit.Timeline.EntityFrameworkCore.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.Timeline.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit timeline entity
/// configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class TimelineModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit Timeline module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureTimelineModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TimelineEntryConfiguration());
        modelBuilder.ApplyConfiguration(new TimelineAttachmentConfiguration());
        return modelBuilder;
    }
}
