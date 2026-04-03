using Granit.DataFiltering;
using Granit.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Granit.Subscriptions.EntityFrameworkCore.Internal;

/// <summary>
/// Dedicated EF Core DbContext for Granit subscriptions and plans.
/// </summary>
internal sealed class SubscriptionsDbContext(
    DbContextOptions<SubscriptionsDbContext> options,
    ICurrentTenant? currentTenant = null,
    IDataFilter? dataFilter = null)
    : DbContext(options)
{
    public DbSet<Plan> Plans { get; set; } = null!;

    public DbSet<Subscription> Subscriptions { get; set; } = null!;

    public DbSet<SubscriptionSeat> Seats { get; set; } = null!;

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ConfigureSubscriptionsModule();
        modelBuilder.ApplyGranitConventions(currentTenant, dataFilter);
    }
}
