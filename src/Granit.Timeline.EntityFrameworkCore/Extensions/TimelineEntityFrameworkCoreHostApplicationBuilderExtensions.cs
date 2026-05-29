using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.Persistence.MultiTenancy;
using Granit.Timeline.Abstractions;
using Granit.Timeline.EntityFrameworkCore.Internal;
using Granit.Timeline.EntityFrameworkCore.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    /// Replaces the default InMemory stores with durable EF Core implementations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Must be called after <c>AddGranitTimeline()</c>. The
    /// <see cref="TimelineEntityFrameworkCoreOptions.StorageMode"/> option chooses how
    /// host-level and tenant-level timeline entries are laid out — see ADR-063.
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <see cref="DualScopeStorageMode.Shared"/> (default) — single host table; tenant
    ///     rows carry <c>TenantId</c> and are filtered by a row-level query filter. Set
    ///     <see cref="TimelineEntityFrameworkCoreOptions.Configure"/>.
    ///   </item>
    ///   <item>
    ///     <see cref="DualScopeStorageMode.Segregated"/> — host rows in the host-pinned
    ///     <c>TimelineHostDbContext</c>, tenant rows in the isolated
    ///     <c>TimelineTenantDbContext</c>. Set
    ///     <see cref="TimelineEntityFrameworkCoreOptions.ConfigureHost"/> plus at least one
    ///     of <see cref="TimelineEntityFrameworkCoreOptions.ConfigureSchemaPerTenant"/> or
    ///     <see cref="TimelineEntityFrameworkCoreOptions.ConfigureDatabasePerTenant"/>.
    ///   </item>
    /// </list>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Configuration callback for the
    /// <see cref="TimelineEntityFrameworkCoreOptions"/>.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="builder"/> or
    /// <paramref name="configure"/> is <c>null</c>.</exception>
    public static IHostApplicationBuilder AddGranitTimelineEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<TimelineEntityFrameworkCoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        TimelineEntityFrameworkCoreOptions options = new();
        configure(options);

        TenantIsolationStrategy strategy = ResolveTenantIsolationStrategy(builder.Configuration);
        DualScopeValidation.ValidateStorageMode(options.StorageMode, strategy, moduleName: "Timeline");

        builder.Services.AddSingleton(options);

        switch (options.StorageMode)
        {
            case DualScopeStorageMode.Shared:
                RegisterSharedMode(builder, options);
                break;

            case DualScopeStorageMode.Segregated:
                RegisterSegregatedMode(builder, options);
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(configure),
                    options.StorageMode,
                    "Unknown DualScopeStorageMode value.");
        }

        RegisterStores(builder.Services);

        return builder;
    }

    private static void RegisterSharedMode(
        IHostApplicationBuilder builder,
        TimelineEntityFrameworkCoreOptions options)
    {
        if (options.Configure is null)
        {
            throw new InvalidOperationException(
                "TimelineEntityFrameworkCoreOptions.Configure must be set when StorageMode is " +
                "DualScopeStorageMode.Shared (the default). Provide an Action<DbContextOptionsBuilder> " +
                "that configures the EF Core provider and connection string for the shared host context.");
        }

        builder.Services.AddGranitDbContext<TimelineHostDbContext>(options.Configure);
        builder.Services.AddHostedService<TimelineDualScopeIntegrationValidator>();

        builder.Services.AddScoped(sp => new TimelineContextResolver(
            DualScopeStorageMode.Shared,
            hostFactory: sp.GetRequiredService<IDbContextFactory<TimelineHostDbContext>>(),
            tenantFactory: null));
    }

    private static void RegisterSegregatedMode(
        IHostApplicationBuilder builder,
        TimelineEntityFrameworkCoreOptions options)
    {
        if (options.ConfigureHost is null)
        {
            throw new InvalidOperationException(
                "TimelineEntityFrameworkCoreOptions.ConfigureHost must be set when StorageMode is " +
                "DualScopeStorageMode.Segregated. Provide an Action<DbContextOptionsBuilder> for the " +
                "host-pinned TimelineHostDbContext.");
        }

        if (options.ConfigureSchemaPerTenant is null && options.ConfigureDatabasePerTenant is null)
        {
            throw new InvalidOperationException(
                "TimelineEntityFrameworkCoreOptions: at least one of ConfigureSchemaPerTenant or " +
                "ConfigureDatabasePerTenant must be set when StorageMode is DualScopeStorageMode.Segregated, " +
                "matching the active TenantIsolationStrategy.");
        }

        builder.Services.AddGranitDbContext<TimelineHostDbContext>(options.ConfigureHost);

        builder.Services.AddGranitIsolatedDbContext<TimelineTenantDbContext>(
            configureShared: _ => { /* SharedDatabase already rejected by DualScopeValidation. */ },
            configureDatabasePerTenant: options.ConfigureDatabasePerTenant,
            configureSchemaPerTenant: options.ConfigureSchemaPerTenant);

        builder.Services.AddScoped(sp => new TimelineContextResolver(
            DualScopeStorageMode.Segregated,
            hostFactory: sp.GetRequiredService<IDbContextFactory<TimelineHostDbContext>>(),
            tenantFactory: sp.GetRequiredService<IDbContextFactory<TimelineTenantDbContext>>()));
    }

    private static void RegisterStores(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<ITimelineWriter, EfCoreTimelineStore>());
        services.Replace(ServiceDescriptor.Scoped<ITimelineReader, EfCoreTimelineQuery>());

        services.AddScoped<EfCoreReactionStore>();
        services.Replace(ServiceDescriptor.Scoped<IReactionReader>(sp => sp.GetRequiredService<EfCoreReactionStore>()));
        services.Replace(ServiceDescriptor.Scoped<IReactionWriter>(sp => sp.GetRequiredService<EfCoreReactionStore>()));
    }

    private static TenantIsolationStrategy ResolveTenantIsolationStrategy(IConfiguration configuration)
    {
        TenantIsolationOptions? bound = configuration
            .GetSection("MultiTenancy:TenantIsolation")
            .Get<TenantIsolationOptions>();

        return bound?.Strategy ?? TenantIsolationStrategy.SharedDatabase;
    }
}
