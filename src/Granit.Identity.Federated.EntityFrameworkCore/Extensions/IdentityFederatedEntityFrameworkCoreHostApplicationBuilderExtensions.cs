using Granit.Identity.Federated.EntityFrameworkCore.Internal;
using Granit.Identity.Federated.EntityFrameworkCore.Options;
using Granit.Identity.Federated.Internal;
using Granit.Identity.Federated.Options;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Identity.Federated.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for enabling EF Core persistence in Granit.Identity.Federated.
/// </summary>
public static class IdentityFederatedEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Replaces the default in-memory / null stores with durable EF Core implementations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Must be called after <c>AddGranitIdentityFederated()</c>.
    /// <see cref="IdentityFederatedEntityFrameworkCoreOptions.StorageMode"/> selects the
    /// physical layout — see ADR-063.
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <see cref="DualScopeStorageMode.Shared"/> (default) — single host table; tenant
    ///     rows carry <c>TenantId</c> and are filtered by a row-level query filter. Set
    ///     <see cref="IdentityFederatedEntityFrameworkCoreOptions.Configure"/>.
    ///   </item>
    ///   <item>
    ///     <see cref="DualScopeStorageMode.Segregated"/> — host rows in
    ///     <see cref="IdentityFederatedHostDbContext"/>, tenant rows in the isolated
    ///     <see cref="IdentityFederatedTenantDbContext"/>. Set
    ///     <see cref="IdentityFederatedEntityFrameworkCoreOptions.ConfigureHost"/> plus
    ///     at least one of
    ///     <see cref="IdentityFederatedEntityFrameworkCoreOptions.ConfigureSchemaPerTenant"/>
    ///     or
    ///     <see cref="IdentityFederatedEntityFrameworkCoreOptions.ConfigureDatabasePerTenant"/>.
    ///   </item>
    /// </list>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Configuration callback for the
    /// <see cref="IdentityFederatedEntityFrameworkCoreOptions"/>.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="builder"/> or
    /// <paramref name="configure"/> is <c>null</c>.</exception>
    public static IHostApplicationBuilder AddGranitIdentityFederatedEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<IdentityFederatedEntityFrameworkCoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        IdentityFederatedEntityFrameworkCoreOptions options = new();
        configure(options);

        TenantIsolationStrategy strategy = ResolveTenantIsolationStrategy(builder.Configuration);
        DualScopeValidation.ValidateStorageMode(options.StorageMode, strategy, moduleName: "Identity.Federated");

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
        IdentityFederatedEntityFrameworkCoreOptions options)
    {
        if (options.Configure is null)
        {
            throw new InvalidOperationException(
                "IdentityFederatedEntityFrameworkCoreOptions.Configure must be set when StorageMode " +
                "is DualScopeStorageMode.Shared (the default). Provide an Action<DbContextOptionsBuilder> " +
                "that configures the EF Core provider and connection string for the shared host context.");
        }

        builder.Services.AddGranitDbContext<IdentityFederatedHostDbContext>(options.Configure);

        builder.Services.AddScoped(sp => new IdentityFederatedContextResolver(
            DualScopeStorageMode.Shared,
            hostFactory: sp.GetRequiredService<IDbContextFactory<IdentityFederatedHostDbContext>>(),
            tenantFactory: null));
    }

    private static void RegisterSegregatedMode(
        IHostApplicationBuilder builder,
        IdentityFederatedEntityFrameworkCoreOptions options)
    {
        if (options.ConfigureHost is null)
        {
            throw new InvalidOperationException(
                "IdentityFederatedEntityFrameworkCoreOptions.ConfigureHost must be set when StorageMode " +
                "is DualScopeStorageMode.Segregated. Provide an Action<DbContextOptionsBuilder> for the " +
                "host-pinned IdentityFederatedHostDbContext.");
        }

        if (options.ConfigureSchemaPerTenant is null && options.ConfigureDatabasePerTenant is null)
        {
            throw new InvalidOperationException(
                "IdentityFederatedEntityFrameworkCoreOptions: at least one of ConfigureSchemaPerTenant or " +
                "ConfigureDatabasePerTenant must be set when StorageMode is DualScopeStorageMode.Segregated, " +
                "matching the active TenantIsolationStrategy.");
        }

        builder.Services.AddGranitDbContext<IdentityFederatedHostDbContext>(options.ConfigureHost);

        builder.Services.AddGranitIsolatedDbContext<IdentityFederatedTenantDbContext>(
            configureShared: _ => { /* SharedDatabase already rejected by DualScopeValidation. */ },
            configureDatabasePerTenant: options.ConfigureDatabasePerTenant,
            configureSchemaPerTenant: options.ConfigureSchemaPerTenant);

        builder.Services.AddScoped(sp => new IdentityFederatedContextResolver(
            DualScopeStorageMode.Segregated,
            hostFactory: sp.GetRequiredService<IDbContextFactory<IdentityFederatedHostDbContext>>(),
            tenantFactory: sp.GetRequiredService<IDbContextFactory<IdentityFederatedTenantDbContext>>()));
    }

    private static void RegisterStores(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Scoped<IUserLookupService, CachedUserLookupService>());
        services.Replace(ServiceDescriptor.Scoped<IUserCacheStats, EfCoreUserCacheStats>());
        services.TryAddScoped<IUserCacheStore, EfCoreUserCacheStore>();
        services.TryAddScoped<IFederatedUserCacheReader, FederatedUserCacheReaderAdapter>();
        services.AddOptions<UserCacheOptions>()
            .BindConfiguration(UserCacheOptions.SectionName);
    }

    private static TenantIsolationStrategy ResolveTenantIsolationStrategy(IConfiguration configuration)
    {
        TenantIsolationOptions? bound = configuration
            .GetSection("MultiTenancy:TenantIsolation")
            .Get<TenantIsolationOptions>();

        return bound?.Strategy ?? TenantIsolationStrategy.SharedDatabase;
    }
}
