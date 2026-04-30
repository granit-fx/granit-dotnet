using Granit.Mergeable.Extensions;
using Granit.Parties.Domain;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.QueryEngine;
using Granit.Subscriptions.Domain;
using Granit.Subscriptions.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Subscriptions.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for Granit subscriptions.
/// </summary>
public static class SubscriptionsEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>Registers EF Core persistence for Granit subscriptions.</summary>
    public static IHostApplicationBuilder AddGranitSubscriptionsEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<SubscriptionsDbContext>(configure);

        builder.Services.TryAddScoped<IPlanReader, EfPlanReader>();
        builder.Services.TryAddScoped<IPlanWriter, EfPlanWriter>();

        builder.Services.TryAddScoped<ISubscriptionReader, EfSubscriptionReader>();
        builder.Services.TryAddScoped<ISubscriptionWriter, EfSubscriptionWriter>();

        builder.Services.TryAddScoped<ISeatReader, EfSeatReader>();
        builder.Services.TryAddScoped<ISeatWriter, EfSeatWriter>();

        builder.Services.TryAddScoped<IPricingResolver, EfPricingResolver>();

        builder.Services.AddScoped<IQueryableSource<Subscription>, EfSubscriptionQueryableSource>();
        builder.Services.AddScoped<IQueryableSource<PlanPrice>, EfPlanPriceQueryableSource>();

        // Plugs Subscriptions into the Party merge orchestrator. Unconditional registration:
        // when no IMergeService<Party> is wired up by the host (e.g. an app without merging),
        // the rewriter just sits idle in DI at zero runtime cost.
        builder.Services.AddReferenceRewriter<Party, SubscriptionPartyReferenceRewriter>();

        return builder;
    }
}
