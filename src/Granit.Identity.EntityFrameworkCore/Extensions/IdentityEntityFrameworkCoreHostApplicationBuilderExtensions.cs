using Granit.Identity.EntityFrameworkCore.Internal;
using Granit.Identity.EntityFrameworkCore.Options;
using Granit.Identity.Internal;
using Granit.Identity.Options;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.Interceptors;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Identity.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for enabling EF Core persistence in Granit.Identity.
/// </summary>
public static class IdentityEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Replaces the default no-op stores with durable EF Core implementations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Must be called after <c>AddGranitIdentity()</c>. The
    /// <see cref="IdentityEntityFrameworkCoreOptions.StorageMode"/> option chooses how
    /// host-admin and tenant users are laid out — see ADR-063.
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <see cref="DualScopeStorageMode.Shared"/> (default) — single host table; tenant
    ///     rows carry <c>TenantId</c> and are filtered by a row-level query filter. Set
    ///     <see cref="IdentityEntityFrameworkCoreOptions.Configure"/>.
    ///   </item>
    ///   <item>
    ///     <see cref="DualScopeStorageMode.Segregated"/> — host-admin rows in the
    ///     host-pinned <c>IdentityHostDbContext</c>, tenant rows in the isolated
    ///     <c>IdentityTenantDbContext</c>. Set
    ///     <see cref="IdentityEntityFrameworkCoreOptions.ConfigureHost"/> plus at least one
    ///     of <see cref="IdentityEntityFrameworkCoreOptions.ConfigureSchemaPerTenant"/> or
    ///     <see cref="IdentityEntityFrameworkCoreOptions.ConfigureDatabasePerTenant"/>.
    ///   </item>
    /// </list>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Configuration callback for the
    /// <see cref="IdentityEntityFrameworkCoreOptions"/>.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="builder"/> or
    /// <paramref name="configure"/> is <c>null</c>.</exception>
    public static IHostApplicationBuilder AddGranitIdentityEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<IdentityEntityFrameworkCoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        IdentityEntityFrameworkCoreOptions options = new();
        configure(options);

        TenantIsolationStrategy strategy = ResolveTenantIsolationStrategy(builder.Configuration);
        DualScopeValidation.ValidateStorageMode(options.StorageMode, strategy, moduleName: "Identity");

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
        IdentityEntityFrameworkCoreOptions options)
    {
        if (options.Configure is null)
        {
            throw new InvalidOperationException(
                "IdentityEntityFrameworkCoreOptions.Configure must be set when StorageMode is " +
                "DualScopeStorageMode.Shared (the default). Provide an Action<DbContextOptionsBuilder> " +
                "that configures the EF Core provider and connection string for the shared host context.");
        }

        builder.Services.AddGranitDbContext<IdentityHostDbContext>(options.Configure);
        builder.Services.AddHostedService<IdentityDualScopeIntegrationValidator>();

        builder.Services.AddScoped(sp => new IdentityContextResolver(
            DualScopeStorageMode.Shared,
            hostFactory: sp.GetRequiredService<IDbContextFactory<IdentityHostDbContext>>(),
            tenantFactory: null));
    }

    private static void RegisterSegregatedMode(
        IHostApplicationBuilder builder,
        IdentityEntityFrameworkCoreOptions options)
    {
        if (options.ConfigureHost is null)
        {
            throw new InvalidOperationException(
                "IdentityEntityFrameworkCoreOptions.ConfigureHost must be set when StorageMode is " +
                "DualScopeStorageMode.Segregated. Provide an Action<DbContextOptionsBuilder> for the " +
                "host-pinned IdentityHostDbContext.");
        }

        if (options.ConfigureSchemaPerTenant is null && options.ConfigureDatabasePerTenant is null)
        {
            throw new InvalidOperationException(
                "IdentityEntityFrameworkCoreOptions: at least one of ConfigureSchemaPerTenant or " +
                "ConfigureDatabasePerTenant must be set when StorageMode is DualScopeStorageMode.Segregated, " +
                "matching the active TenantIsolationStrategy.");
        }

        builder.Services.AddGranitDbContext<IdentityHostDbContext>(options.ConfigureHost);

        builder.Services.AddGranitIsolatedDbContext<IdentityTenantDbContext>(
            configureShared: _ => { /* SharedDatabase already rejected by DualScopeValidation. */ },
            configureDatabasePerTenant: options.ConfigureDatabasePerTenant,
            configureSchemaPerTenant: options.ConfigureSchemaPerTenant);

        builder.Services.AddScoped(sp => new IdentityContextResolver(
            DualScopeStorageMode.Segregated,
            hostFactory: sp.GetRequiredService<IDbContextFactory<IdentityHostDbContext>>(),
            tenantFactory: sp.GetRequiredService<IDbContextFactory<IdentityTenantDbContext>>()));
    }

    private static void RegisterStores(IServiceCollection services)
    {
        services.TryAddScoped<IUserDirectoryQueryableSource, EfUserDirectoryQueryableSource>();
        services.TryAddScoped<IUserDirectoryWriter, EfUserDirectoryWriter>();

        // Lookup hasher backing User.EmailHash / User.PhoneNumberHash. Pepper validated at
        // first resolution (HmacUserLookupHasher ctor) — fail-fast on missing configuration
        // so production deployments cannot run with a known-zero key.
        services.AddOptions<UserLookupHasherOptions>()
            .BindConfiguration(UserLookupHasherOptions.SectionName);
        services.TryAddSingleton<IUserLookupHasher, HmacUserLookupHasher>();

        // Save-time interceptor that recomputes digests in lockstep with the encrypted
        // plaintext columns. Registered via IGranitAutoInterceptor (same pattern as
        // AuditingChangeTrackingInterceptor) so any DbContext wired through
        // AddGranitDbContext / AddGranitIsolatedDbContext picks it up.
        services.AddScoped<UserLookupHashInterceptor>();
        services.AddScoped<IGranitAutoInterceptor>(sp =>
            sp.GetRequiredService<UserLookupHashInterceptor>());
    }

    private static TenantIsolationStrategy ResolveTenantIsolationStrategy(IConfiguration configuration)
    {
        TenantIsolationOptions? bound = configuration
            .GetSection("MultiTenancy:TenantIsolation")
            .Get<TenantIsolationOptions>();

        return bound?.Strategy ?? TenantIsolationStrategy.SharedDatabase;
    }
}
