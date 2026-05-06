using Granit.Activities.EntityFrameworkCore.Internal;
using Granit.Activities.Persistence;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Granit.Activities.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for enabling EF Core persistence in Granit.Activities.
/// </summary>
public static class ActivitiesEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the isolated <see cref="ActivitiesDbContext"/> via
    /// <c>AddGranitDbContext</c> (which wires audit + soft-delete + lifecycle
    /// interceptors automatically).
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core <see cref="DbContextOptionsBuilder"/> configuration (provider + connection string).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitActivitiesEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddGranitDbContext<ActivitiesDbContext>(configure);
        builder.Services.AddScoped<IActivityReader, EfCoreActivityReader>();
        builder.Services.AddScoped<IActivityWriter, EfCoreActivityWriter>();
        return builder;
    }
}
