using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Timeline.Abstractions;
using Granit.Timeline.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Timeline.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for enabling EF Core persistence in Granit.Timeline.
/// </summary>
public static class TimelineEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Replaces the default InMemory stores with durable EF Core implementations
    /// backed by a PostgreSQL database.
    /// </summary>
    /// <remarks>
    /// Must be called after <c>AddGranitTimeline()</c>.
    /// Registers:
    /// <list type="bullet">
    ///   <item><see cref="EfCoreTimelineStore"/> — replaces <c>InMemoryTimelineStore</c>.</item>
    ///   <item><see cref="EfCoreTimelineQuery"/> — replaces <c>InMemoryTimelineQuery</c>.</item>
    ///   <item><see cref="Internal.TimelineDbContext"/> — registered via <c>IDbContextFactory</c> for thread-safe usage.</item>
    /// </list>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core <see cref="DbContextOptionsBuilder"/> configuration (provider + connection string).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitTimelineEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<TimelineDbContext>(configure);

        builder.Services.Replace(
            ServiceDescriptor.Scoped<ITimelineWriter, EfCoreTimelineStore>());
        builder.Services.Replace(
            ServiceDescriptor.Scoped<ITimelineReader, EfCoreTimelineQuery>());

        // Reactions (story C1) — single EF impl backs both reader + writer.
        builder.Services.AddScoped<EfCoreReactionStore>();
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IReactionReader>(sp => sp.GetRequiredService<EfCoreReactionStore>()));
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IReactionWriter>(sp => sp.GetRequiredService<EfCoreReactionStore>()));

        return builder;
    }
}
