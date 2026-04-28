using Granit.Dashboards.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Granit.Dashboards.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for the Granit Dashboards module.
/// </summary>
public static class DashboardsEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the isolated <see cref="DashboardsDbContext"/> via
    /// <c>AddGranitDbContext</c> so the standard interceptors (auditing, soft-delete)
    /// are wired automatically.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core provider configuration (e.g. <c>options.UseNpgsql(conn)</c>).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitDashboardsEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddGranitDbContext<DashboardsDbContext>(configure);
        return builder;
    }
}
