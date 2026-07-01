using Granit.Notifications.WebPush.EntityFrameworkCore.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.Notifications.WebPush.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including the browser push subscription
/// entity configuration in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class WebPushModelBuilderExtensions
{
    /// <summary>
    /// Applies the entity configuration for the Granit Web Push channel.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureWebPushModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new WebPushSubscriptionConfiguration());
        return modelBuilder;
    }
}
