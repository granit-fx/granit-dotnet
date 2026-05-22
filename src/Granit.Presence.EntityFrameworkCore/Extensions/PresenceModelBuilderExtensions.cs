using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Presence.EntityFrameworkCore.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.Presence.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including the Granit presence entity
/// configuration in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class PresenceModelBuilderExtensions
{
    /// <summary>
    /// Applies the entity configuration for the Granit Presence module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigurePresenceModule(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyGranitConventions();
        modelBuilder.ApplyConfiguration(new UserPresenceConfiguration());
        return modelBuilder;
    }
}
