using Granit.Notifications.Abstractions;
using Granit.Notifications.EntityFrameworkCore.Internal;
using Granit.Notifications.EntityFrameworkCore.Options;
using Granit.Notifications.Internal;
using Granit.Notifications.MobilePush;
using Granit.Notifications.MobilePush.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.Persistence.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Notifications.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for enabling EF Core persistence in Granit.Notifications.
/// </summary>
public static class NotificationsEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Replaces the default InMemory/no-op stores with durable EF Core implementations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Must be called after <c>AddGranitNotifications()</c>. The
    /// <see cref="NotificationsEntityFrameworkCoreOptions.StorageMode"/> option chooses how
    /// host-level and tenant-level notifications are laid out — see ADR-063.
    /// </para>
    /// <list type="bullet">
    ///   <item>
    ///     <see cref="DualScopeStorageMode.Shared"/> (default) — single host table; tenant
    ///     rows carry <c>TenantId</c> and are filtered by a row-level query filter.
    ///   </item>
    ///   <item>
    ///     <see cref="DualScopeStorageMode.Segregated"/> — host rows in
    ///     <c>NotificationsHostDbContext</c>, tenant rows in the isolated
    ///     <c>NotificationsTenantDbContext</c>. NotificationDeliveryAttempt audit rows
    ///     always land in the host context (centralised SOC2 trail), regardless of the
    ///     parent notification's scope.
    ///   </item>
    /// </list>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Configuration callback for
    /// <see cref="NotificationsEntityFrameworkCoreOptions"/>.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitNotificationsEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<NotificationsEntityFrameworkCoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        NotificationsEntityFrameworkCoreOptions options = new();
        configure(options);

        TenantIsolationStrategy strategy = ResolveTenantIsolationStrategy(builder.Configuration);
        DualScopeValidation.ValidateStorageMode(options.StorageMode, strategy, moduleName: "Notifications");

        builder.Services.AddSingleton(options);

        // EntityTrackingInterceptor implements IGranitAutoInterceptor — registered with
        // both the concrete type and the auto-interceptor marker so UseGranitInterceptors
        // (invoked by AddGranitDbContext / AddGranitIsolatedDbContext) picks it up.
        builder.Services.TryAddScoped<EntityTrackingInterceptor>();
        builder.Services.AddScoped<Persistence.EntityFrameworkCore.Interceptors.IGranitAutoInterceptor>(sp =>
            sp.GetRequiredService<EntityTrackingInterceptor>());

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
        NotificationsEntityFrameworkCoreOptions options)
    {
        if (options.Configure is null)
        {
            throw new InvalidOperationException(
                "NotificationsEntityFrameworkCoreOptions.Configure must be set when StorageMode is " +
                "DualScopeStorageMode.Shared (the default). Provide an Action<DbContextOptionsBuilder> " +
                "that configures the EF Core provider and connection string.");
        }

        builder.Services.AddGranitDbContext<NotificationsHostDbContext>(options.Configure);
        builder.Services.AddHostedService<NotificationsDualScopeIntegrationValidator>();

        builder.Services.AddScoped(sp => new NotificationsContextResolver(
            DualScopeStorageMode.Shared,
            hostFactory: sp.GetRequiredService<IDbContextFactory<NotificationsHostDbContext>>(),
            tenantFactory: null));
    }

    private static void RegisterSegregatedMode(
        IHostApplicationBuilder builder,
        NotificationsEntityFrameworkCoreOptions options)
    {
        if (options.ConfigureHost is null)
        {
            throw new InvalidOperationException(
                "NotificationsEntityFrameworkCoreOptions.ConfigureHost must be set when StorageMode is " +
                "DualScopeStorageMode.Segregated.");
        }

        if (options.ConfigureSchemaPerTenant is null && options.ConfigureDatabasePerTenant is null)
        {
            throw new InvalidOperationException(
                "NotificationsEntityFrameworkCoreOptions: at least one of ConfigureSchemaPerTenant or " +
                "ConfigureDatabasePerTenant must be set when StorageMode is DualScopeStorageMode.Segregated.");
        }

        builder.Services.AddGranitDbContext<NotificationsHostDbContext>(options.ConfigureHost);

        builder.Services.AddGranitIsolatedDbContext<NotificationsTenantDbContext>(
            configureShared: _ => { /* SharedDatabase already rejected by DualScopeValidation. */ },
            configureDatabasePerTenant: options.ConfigureDatabasePerTenant,
            configureSchemaPerTenant: options.ConfigureSchemaPerTenant);

        builder.Services.AddScoped(sp => new NotificationsContextResolver(
            DualScopeStorageMode.Segregated,
            hostFactory: sp.GetRequiredService<IDbContextFactory<NotificationsHostDbContext>>(),
            tenantFactory: sp.GetRequiredService<IDbContextFactory<NotificationsTenantDbContext>>()));
    }

    private static void RegisterStores(IServiceCollection services)
    {
        // UserNotification — CQRS forwarding
        services.RemoveAll<InMemoryUserNotificationStore>();
        services.AddScoped<EfCoreUserNotificationStore>();
        services.Replace(ServiceDescriptor.Scoped<IUserNotificationReader>(sp => sp.GetRequiredService<EfCoreUserNotificationStore>()));
        services.Replace(ServiceDescriptor.Scoped<IUserNotificationWriter>(sp => sp.GetRequiredService<EfCoreUserNotificationStore>()));

        // Preference — CQRS forwarding
        services.RemoveAll<InMemoryNotificationPreferenceStore>();
        services.AddScoped<EfCoreNotificationPreferenceStore>();
        services.Replace(ServiceDescriptor.Scoped<INotificationPreferenceReader>(sp => sp.GetRequiredService<EfCoreNotificationPreferenceStore>()));
        services.Replace(ServiceDescriptor.Scoped<INotificationPreferenceWriter>(sp => sp.GetRequiredService<EfCoreNotificationPreferenceStore>()));

        // Subscription — CQRS forwarding
        services.RemoveAll<InMemoryNotificationSubscriptionStore>();
        services.AddScoped<EfCoreNotificationSubscriptionStore>();
        services.Replace(ServiceDescriptor.Scoped<INotificationSubscriptionReader>(sp => sp.GetRequiredService<EfCoreNotificationSubscriptionStore>()));
        services.Replace(ServiceDescriptor.Scoped<INotificationSubscriptionWriter>(sp => sp.GetRequiredService<EfCoreNotificationSubscriptionStore>()));

        // Delivery — write-only ISO 27001 audit (always host-routed)
        services.Replace(ServiceDescriptor.Scoped<INotificationDeliveryWriter, EfCoreNotificationDeliveryStore>());

        // MobilePush — CQRS forwarding
        services.RemoveAll<InMemoryMobilePushTokenStore>();
        services.AddScoped<EfCoreMobilePushTokenStore>();
        services.Replace(ServiceDescriptor.Scoped<IMobilePushTokenReader>(sp => sp.GetRequiredService<EfCoreMobilePushTokenStore>()));
        services.Replace(ServiceDescriptor.Scoped<IMobilePushTokenWriter>(sp => sp.GetRequiredService<EfCoreMobilePushTokenStore>()));
    }

    private static TenantIsolationStrategy ResolveTenantIsolationStrategy(IConfiguration configuration)
    {
        TenantIsolationOptions? bound = configuration
            .GetSection("MultiTenancy:TenantIsolation")
            .Get<TenantIsolationOptions>();

        return bound?.Strategy ?? TenantIsolationStrategy.SharedDatabase;
    }
}
