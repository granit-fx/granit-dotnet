using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Subscriptions.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core persistence of subscriptions and plans.
/// </summary>
[DependsOn(
    typeof(GranitPersistenceEntityFrameworkCoreModule),
    typeof(GranitSubscriptionsModule))]
public sealed class GranitSubscriptionsEntityFrameworkCoreModule : GranitModule;
