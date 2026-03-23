using Granit.BackgroundJobs;
using Granit.Bff.EntityFrameworkCore.Internal;
using Granit.Core.Modularity;
using Granit.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Bff.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core-backed BFF session persistence.
/// Replaces the default <see cref="IDistributedCache"/>-backed <c>IBffTokenStore</c>
/// for deployments without Redis.
/// </summary>
[DependsOn(
    typeof(GranitBackgroundJobsModule),
    typeof(GranitBffModule),
    typeof(GranitPersistenceModule))]
public sealed class GranitBffEntityFrameworkCoreModule : GranitModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Replace the default distributed cache store with EF Core
        context.Services.AddScoped<IBffTokenStore, EfCoreBffTokenStore>();
    }
}
