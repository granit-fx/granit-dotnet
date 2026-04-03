using Granit.Persistence.EntityFrameworkCore.Extensions;
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
        builder.Services.AddInternalDbContextEnsurer<SubscriptionsDbContext>();

        builder.Services.AddScoped<EfPlanStore>();
        builder.Services.Replace(ServiceDescriptor.Scoped<IPlanReader>(sp => sp.GetRequiredService<EfPlanStore>()));
        builder.Services.Replace(ServiceDescriptor.Scoped<IPlanWriter>(sp => sp.GetRequiredService<EfPlanStore>()));

        builder.Services.AddScoped<EfSubscriptionStore>();
        builder.Services.Replace(ServiceDescriptor.Scoped<ISubscriptionReader>(sp => sp.GetRequiredService<EfSubscriptionStore>()));
        builder.Services.Replace(ServiceDescriptor.Scoped<ISubscriptionWriter>(sp => sp.GetRequiredService<EfSubscriptionStore>()));

        return builder;
    }
}
