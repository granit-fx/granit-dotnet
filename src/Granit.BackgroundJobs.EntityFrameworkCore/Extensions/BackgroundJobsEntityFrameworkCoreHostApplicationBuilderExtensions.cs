using Granit.BackgroundJobs.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.BackgroundJobs.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for Granit background jobs.
/// </summary>
public static class BackgroundJobsEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers EF Core persistence for Granit background jobs.
    /// </summary>
    /// <remarks>
    /// Replaces the default <c>InMemoryBackgroundJobStore</c> registered by
    /// <c>AddGranitBackgroundJobs()</c> with <see cref="EfBackgroundJobStore"/>,
    /// and registers <see cref="BackgroundJobsDbContext"/> via
    /// <c>AddGranitDbContext</c> (<c>IDbContextFactory</c> with interceptor DI).
    /// <para>
    /// Must be called after <c>AddGranitBackgroundJobs()</c>.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core <see cref="DbContextOptionsBuilder"/> configuration (provider + connection string).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitBackgroundJobsEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddGranitDbContext<BackgroundJobsDbContext>(configure);

        // Scoped: AddGranitDbContext registers IDbContextFactory<T> as Scoped (interceptors depend on
        // ICurrentTenant/ICurrentUser which are Scoped). EfBackgroundJobStore injects the factory and
        // must therefore also be Scoped to avoid captive dependency violations.
        builder.Services.AddScoped<EfBackgroundJobStore>();
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IBackgroundJobStoreReader>(sp => sp.GetRequiredService<EfBackgroundJobStore>()));
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IBackgroundJobStoreWriter>(sp => sp.GetRequiredService<EfBackgroundJobStore>()));

        return builder;
    }
}
