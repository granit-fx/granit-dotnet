using Granit.Auditing.Domain;
using Granit.Auditing.EntityFrameworkCore.Interceptors;
using Granit.Auditing.EntityFrameworkCore.Internal;
using Granit.Auditing.EntityFrameworkCore.Internal.Services;
using Granit.Auditing.EntityFrameworkCore.Options;
using Granit.Auditing.Internal.Services;
using Granit.Auditing.Options;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.Interceptors;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.Persistence.MultiTenancy;
using Granit.QueryEngine;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.Auditing.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering EF Core persistence for Granit audit logging.
/// </summary>
public static class AuditingEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers EF Core persistence for the Granit audit log module.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="AuditingEntityFrameworkCoreOptions.StorageMode"/> option chooses how
    /// host-level and tenant-level audit entries are laid out — see ADR-063.
    /// </para>
    /// <para>
    /// The <see cref="AuditingChangeTrackingInterceptor"/> is added to the consumer's host
    /// DbContext separately via
    /// <see cref="DbContextOptionsBuilderAuditingExtensions.UseGranitAuditingInterceptor"/>
    /// or auto-wired via <see cref="IGranitAutoInterceptor"/>. It must NOT be registered
    /// against the Auditing host/tenant contexts themselves — that would cause infinite
    /// recursion.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Configuration callback for
    /// <see cref="AuditingEntityFrameworkCoreOptions"/>.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="builder"/> or
    /// <paramref name="configure"/> is <c>null</c>.</exception>
    public static IHostApplicationBuilder AddGranitAuditingEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<AuditingEntityFrameworkCoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        AuditingEntityFrameworkCoreOptions options = new();
        configure(options);

        TenantIsolationStrategy strategy = ResolveTenantIsolationStrategy(builder.Configuration);
        DualScopeValidation.ValidateStorageMode(options.StorageMode, strategy, moduleName: "Auditing");

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

        RegisterServices(builder.Services);

        return builder;
    }

    private static void RegisterSharedMode(
        IHostApplicationBuilder builder,
        AuditingEntityFrameworkCoreOptions options)
    {
        if (options.Configure is null)
        {
            throw new InvalidOperationException(
                "AuditingEntityFrameworkCoreOptions.Configure must be set when StorageMode is " +
                "DualScopeStorageMode.Shared (the default). Provide an Action<DbContextOptionsBuilder> " +
                "that configures the EF Core provider and connection string.");
        }

        builder.Services.AddGranitDbContext<AuditingHostDbContext>(options.Configure);
        builder.Services.AddHostedService<AuditingDualScopeIntegrationValidator>();

        builder.Services.AddScoped(sp => new AuditingContextResolver(
            DualScopeStorageMode.Shared,
            hostFactory: sp.GetRequiredService<IDbContextFactory<AuditingHostDbContext>>(),
            tenantFactory: null));
    }

    private static void RegisterSegregatedMode(
        IHostApplicationBuilder builder,
        AuditingEntityFrameworkCoreOptions options)
    {
        if (options.ConfigureHost is null)
        {
            throw new InvalidOperationException(
                "AuditingEntityFrameworkCoreOptions.ConfigureHost must be set when StorageMode is " +
                "DualScopeStorageMode.Segregated.");
        }

        if (options.ConfigureSchemaPerTenant is null && options.ConfigureDatabasePerTenant is null)
        {
            throw new InvalidOperationException(
                "AuditingEntityFrameworkCoreOptions: at least one of ConfigureSchemaPerTenant or " +
                "ConfigureDatabasePerTenant must be set when StorageMode is DualScopeStorageMode.Segregated.");
        }

        builder.Services.AddGranitDbContext<AuditingHostDbContext>(options.ConfigureHost);

        builder.Services.AddGranitIsolatedDbContext<AuditingTenantDbContext>(
            configureShared: _ => { /* SharedDatabase already rejected by DualScopeValidation. */ },
            configureDatabasePerTenant: options.ConfigureDatabasePerTenant,
            configureSchemaPerTenant: options.ConfigureSchemaPerTenant);

        builder.Services.AddScoped(sp => new AuditingContextResolver(
            DualScopeStorageMode.Segregated,
            hostFactory: sp.GetRequiredService<IDbContextFactory<AuditingHostDbContext>>(),
            tenantFactory: sp.GetRequiredService<IDbContextFactory<AuditingTenantDbContext>>()));
    }

    private static void RegisterServices(IServiceCollection services)
    {
        // Publisher: async (Channel) or strict (synchronous).
        services.AddScoped<StrictAuditingPublisher>();
        services.AddScoped<IAuditEntryPublisher>(sp =>
        {
            AuditingOptions opts = sp.GetRequiredService<IOptions<AuditingOptions>>().Value;
            return opts.PersistenceMode == AuditPersistenceMode.Strict
                ? sp.GetRequiredService<StrictAuditingPublisher>()
                : sp.GetRequiredService<ChannelAuditingPublisher>();
        });

        // EF Core implementations of persistence abstractions.
        services.AddScoped<IAuditBatchPersister, EfCoreAuditBatchPersister>();
        services.AddScoped<IAuditingCleaner, EfCoreAuditingCleaner>();

        // CQRS services.
        services.AddScoped<IAuditingReader, EfCoreAuditingReader>();
        services.AddScoped<IAuditingWriter, EfCoreAuditingWriter>();

        // Queryable sources for MapGranitQuery (host bypasses tenant filter for cross-tenant audit review).
        services.AddScoped<IQueryableSource<AuditEntry>, EfAuditEntryQueryableSource>();
        services.AddScoped<IQueryableSource<AuditEntityChange>, EfAuditEntityChangeQueryableSource>();

        // HttpContextAccessor for IP/UserAgent capture in audit entries.
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();

        // Interceptor + capture service (scoped — host DbContext resolves from its SP).
        // Registered as both concrete type (for UseGranitAuditingInterceptor backward compat)
        // and IGranitAutoInterceptor (for automatic wiring via UseGranitInterceptors).
        services.AddScoped<AuditingChangeTrackingInterceptor>();
        services.AddScoped<IGranitAutoInterceptor>(sp =>
            sp.GetRequiredService<AuditingChangeTrackingInterceptor>());
        services.AddScoped<ChangeTrackingCaptureService>();
    }

    private static TenantIsolationStrategy ResolveTenantIsolationStrategy(IConfiguration configuration)
    {
        TenantIsolationOptions? bound = configuration
            .GetSection("MultiTenancy:TenantIsolation")
            .Get<TenantIsolationOptions>();

        return bound?.Strategy ?? TenantIsolationStrategy.SharedDatabase;
    }
}
