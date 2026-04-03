using Granit.Subscriptions.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;

namespace Granit.Subscriptions.EntityFrameworkCore.Extensions;

/// <summary>
/// <see cref="ModelBuilder"/> extensions for including Granit subscriptions entity configurations.
/// </summary>
public static class SubscriptionsModelBuilderExtensions
{
    /// <summary>Applies all entity configurations for the Granit Subscriptions module.</summary>
    public static ModelBuilder ConfigureSubscriptionsModule(this ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new PlanConfiguration());
        modelBuilder.ApplyConfiguration(new PlanPriceConfiguration());
        modelBuilder.ApplyConfiguration(new PlanFeatureValueConfiguration());
        modelBuilder.ApplyConfiguration(new PlanExternalMappingConfiguration());
        modelBuilder.ApplyConfiguration(new SubscriptionConfiguration());
        modelBuilder.ApplyConfiguration(new SubscriptionSeatConfiguration());
        modelBuilder.ApplyConfiguration(new SubscriptionExternalMappingConfiguration());
        return modelBuilder;
    }
}
