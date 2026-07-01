using Granit.Notifications.MobilePush.EntityFrameworkCore.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.MobilePush.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including the mobile push token entity
/// configuration in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class MobilePushModelBuilderExtensions
{
    /// <summary>
    /// Applies the entity configuration for the Granit mobile push channel.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureMobilePushModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new MobilePushTokenConfiguration());
        return modelBuilder;
    }
}
