using Granit.Webhooks.EntityFrameworkCore.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Granit.Webhooks.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit webhook entity
/// configurations in a host-owned <see cref="DbContext"/>.
/// </summary>
public static class WebhooksModelBuilderExtensions
{
    /// <summary>
    /// Applies all entity configurations for the Granit Webhooks module.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <returns>The same <paramref name="modelBuilder"/> for chaining.</returns>
    public static ModelBuilder ConfigureWebhooksModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new WebhookSubscriptionConfiguration());
        modelBuilder.ApplyConfiguration(new WebhookSigningKeyConfiguration());
        modelBuilder.ApplyConfiguration(new WebhookDeliveryAttemptConfiguration());
        return modelBuilder;
    }
}
