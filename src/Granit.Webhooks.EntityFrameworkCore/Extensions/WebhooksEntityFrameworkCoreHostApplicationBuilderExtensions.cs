using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.Persistence.MultiTenancy;
using Granit.QueryEngine;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Domain;
using Granit.Webhooks.EntityFrameworkCore.Internal;
using Granit.Webhooks.EntityFrameworkCore.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Webhooks.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for enabling EF Core persistence in Granit.Webhooks.
/// </summary>
public static class WebhooksEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Replaces the default InMemory/no-op stores with durable EF Core implementations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Must be called after <c>AddGranitWebhooks()</c>. The
    /// <see cref="WebhooksEntityFrameworkCoreOptions.StorageMode"/> option chooses how
    /// host-scope and tenant-scope subscriptions are laid out — see ADR-063.
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <see cref="DualScopeStorageMode.Shared"/> (default) — single host table; tenant
    ///     rows carry <c>TenantId</c> and are filtered by a row-level query filter. Set
    ///     <see cref="WebhooksEntityFrameworkCoreOptions.Configure"/>.
    ///   </item>
    ///   <item>
    ///     <see cref="DualScopeStorageMode.Segregated"/> — host rows in
    ///     <see cref="WebhooksHostDbContext"/>, tenant rows in the isolated
    ///     <see cref="WebhooksTenantDbContext"/>. Set
    ///     <see cref="WebhooksEntityFrameworkCoreOptions.ConfigureHost"/> plus at least one
    ///     of <see cref="WebhooksEntityFrameworkCoreOptions.ConfigureSchemaPerTenant"/> or
    ///     <see cref="WebhooksEntityFrameworkCoreOptions.ConfigureDatabasePerTenant"/>.
    ///   </item>
    /// </list>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Configuration callback for the <see cref="WebhooksEntityFrameworkCoreOptions"/>.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitWebhooksEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<WebhooksEntityFrameworkCoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        WebhooksEntityFrameworkCoreOptions options = new();
        configure(options);

        TenantIsolationStrategy strategy = ResolveTenantIsolationStrategy(builder.Configuration);
        DualScopeValidation.ValidateStorageMode(options.StorageMode, strategy, moduleName: "Webhooks");

        // Singleton: options carry the storage choice consulted by every scoped service.
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
        WebhooksEntityFrameworkCoreOptions options)
    {
        if (options.Configure is null)
        {
            throw new InvalidOperationException(
                "WebhooksEntityFrameworkCoreOptions.Configure must be set when StorageMode is " +
                "DualScopeStorageMode.Shared (the default). Provide an Action<DbContextOptionsBuilder> " +
                "that configures the EF Core provider and connection string for the shared host context.");
        }

        builder.Services.AddGranitDbContext<WebhooksHostDbContext>(options.Configure);
        builder.Services.AddHostedService<WebhooksDualScopeIntegrationValidator>();

        builder.Services.AddScoped(sp => new WebhooksContextResolver(
            DualScopeStorageMode.Shared,
            hostFactory: sp.GetRequiredService<IDbContextFactory<WebhooksHostDbContext>>(),
            tenantFactory: null));
    }

    private static void RegisterSegregatedMode(
        IHostApplicationBuilder builder,
        WebhooksEntityFrameworkCoreOptions options)
    {
        if (options.ConfigureHost is null)
        {
            throw new InvalidOperationException(
                "WebhooksEntityFrameworkCoreOptions.ConfigureHost must be set when StorageMode is " +
                "DualScopeStorageMode.Segregated. Provide an Action<DbContextOptionsBuilder> for the " +
                "host-pinned WebhooksHostDbContext.");
        }

        if (options.ConfigureSchemaPerTenant is null && options.ConfigureDatabasePerTenant is null)
        {
            throw new InvalidOperationException(
                "WebhooksEntityFrameworkCoreOptions: at least one of ConfigureSchemaPerTenant or " +
                "ConfigureDatabasePerTenant must be set when StorageMode is DualScopeStorageMode.Segregated, " +
                "matching the active TenantIsolationStrategy.");
        }

        builder.Services.AddGranitDbContext<WebhooksHostDbContext>(options.ConfigureHost);

        builder.Services.AddGranitIsolatedDbContext<WebhooksTenantDbContext>(
            configureShared: _ => { /* SharedDatabase already rejected by DualScopeValidation. */ },
            configureDatabasePerTenant: options.ConfigureDatabasePerTenant,
            configureSchemaPerTenant: options.ConfigureSchemaPerTenant);

        builder.Services.AddScoped(sp => new WebhooksContextResolver(
            DualScopeStorageMode.Segregated,
            hostFactory: sp.GetRequiredService<IDbContextFactory<WebhooksHostDbContext>>(),
            tenantFactory: sp.GetRequiredService<IDbContextFactory<WebhooksTenantDbContext>>()));
    }

    private static void RegisterStores(IServiceCollection services)
    {
        services.AddScoped<EfWebhookSubscriptionStore>();
        services.Replace(ServiceDescriptor.Scoped<IWebhookSubscriptionReader>(sp => sp.GetRequiredService<EfWebhookSubscriptionStore>()));
        services.Replace(ServiceDescriptor.Scoped<IWebhookSubscriptionWriter>(sp => sp.GetRequiredService<EfWebhookSubscriptionStore>()));
        services.Replace(ServiceDescriptor.Scoped<IWebhookSigningKeyReader>(sp => sp.GetRequiredService<EfWebhookSubscriptionStore>()));
        services.Replace(ServiceDescriptor.Scoped<IWebhookSigningKeyWriter>(sp => sp.GetRequiredService<EfWebhookSubscriptionStore>()));

        services.Replace(ServiceDescriptor.Scoped<IWebhookDeliveryWriter, EfWebhookDeliveryStore>());
        services.Replace(ServiceDescriptor.Scoped<IWebhookDeliveryReader, EfWebhookDeliveryStore>());

        services.Replace(ServiceDescriptor.Scoped<IWebhookStatsReader, EfWebhookStatsReader>());

        services.AddScoped<IQueryableSource<WebhookSubscription>, EfWebhookSubscriptionQueryableSource>();
        services.AddScoped<IQueryableSource<WebhookDeliveryAttempt>, EfWebhookDeliveryAttemptQueryableSource>();
    }

    private static TenantIsolationStrategy ResolveTenantIsolationStrategy(IConfiguration configuration)
    {
        TenantIsolationOptions? bound = configuration
            .GetSection("MultiTenancy:TenantIsolation")
            .Get<TenantIsolationOptions>();

        return bound?.Strategy ?? TenantIsolationStrategy.SharedDatabase;
    }
}
