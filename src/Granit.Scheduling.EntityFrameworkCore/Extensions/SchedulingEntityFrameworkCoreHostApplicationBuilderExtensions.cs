using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Scheduling.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Scheduling.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for Granit scheduling.
/// </summary>
public static class SchedulingEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers EF Core persistence for Granit scheduled actions.
    /// </summary>
    /// <remarks>
    /// Registers <see cref="SchedulingDbContext"/> via <c>AddGranitDbContext</c>
    /// (<c>IDbContextFactory</c> with interceptor DI) and replaces any existing
    /// <see cref="IScheduledActionReader"/>/<see cref="IScheduledActionWriter"/>
    /// registrations with the EF Core implementation.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core <see cref="DbContextOptionsBuilder"/> configuration (provider + connection string).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitSchedulingEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<SchedulingDbContext>(configure);
        builder.Services.AddInternalDbContextEnsurer<SchedulingDbContext>();

        builder.Services.AddScoped<EfScheduledActionStore>();
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IScheduledActionReader>(sp => sp.GetRequiredService<EfScheduledActionStore>()));
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IScheduledActionWriter>(sp => sp.GetRequiredService<EfScheduledActionStore>()));

        builder.Services.AddScoped<IScheduledActionQueryableProvider, EfScheduledActionQueryableProvider>();

        return builder;
    }
}
