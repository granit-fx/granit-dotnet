using Granit.Notifications.Abstractions;
using Granit.Notifications.EntityFrameworkCore.Internal;
using Granit.Notifications.Internal;
using Granit.Notifications.MobilePush;
using Granit.Notifications.MobilePush.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

// EntityTrackingInterceptor requires manual wiring because UseGranitInterceptors
// only resolves the 6 standard Granit.Persistence interceptors.

namespace Granit.Notifications.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for enabling EF Core persistence in Granit.Notifications.
/// </summary>
public static class NotificationsEfCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Replaces the default InMemory/no-op stores with durable EF Core implementations
    /// backed by a PostgreSQL database.
    /// </summary>
    /// <remarks>
    /// Must be called after <c>AddGranitNotifications()</c>.
    /// Registers:
    /// <list type="bullet">
    ///   <item><see cref="EfCoreUserNotificationStore"/> — replaces <c>InMemoryUserNotificationStore</c>.</item>
    ///   <item><see cref="EfCoreNotificationPreferenceStore"/> — replaces <c>InMemoryNotificationPreferenceStore</c>.</item>
    ///   <item><see cref="EfCoreNotificationSubscriptionStore"/> — replaces <c>InMemoryNotificationSubscriptionStore</c>.</item>
    ///   <item><see cref="EfCoreNotificationDeliveryStore"/> — replaces <c>NullNotificationDeliveryStore</c> (enables ISO 27001 audit trail).</item>
    ///   <item><see cref="EfCoreMobilePushTokenStore"/> — replaces <c>InMemoryMobilePushTokenStore</c>.</item>
    ///   <item><see cref="Internal.NotificationsDbContext"/> — registered via <c>IDbContextFactory</c> for thread-safe usage in Wolverine handlers.</item>
    /// </list>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core <see cref="DbContextOptionsBuilder"/> configuration (provider + connection string).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitNotificationsEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.TryAddScoped<EntityTrackingInterceptor>();
        builder.Services.AddDbContextFactory<NotificationsDbContext>((sp, options) =>
        {
            configure(options);
            options.UseGranitInterceptors(sp);
            options.AddInterceptors(sp.GetRequiredService<EntityTrackingInterceptor>());
        }, ServiceLifetime.Scoped);
        builder.Services.AddHostInternalDbContextEnsurer<NotificationsDbContext>();

        // UserNotification store — CQRS forwarding pattern
        // Scoped: AddGranitDbContext registers IDbContextFactory<T> as Scoped (interceptors
        // depend on ICurrentTenant/ICurrentUser which are Scoped). Stores injecting the factory
        // must also be Scoped to avoid captive dependency violations.
        builder.Services.RemoveAll<InMemoryUserNotificationStore>();
        builder.Services.AddScoped<EfCoreUserNotificationStore>();
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IUserNotificationReader>(sp => sp.GetRequiredService<EfCoreUserNotificationStore>()));
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IUserNotificationWriter>(sp => sp.GetRequiredService<EfCoreUserNotificationStore>()));

        // Preference store — CQRS forwarding pattern
        builder.Services.RemoveAll<InMemoryNotificationPreferenceStore>();
        builder.Services.AddScoped<EfCoreNotificationPreferenceStore>();
        builder.Services.Replace(
            ServiceDescriptor.Scoped<INotificationPreferenceReader>(sp => sp.GetRequiredService<EfCoreNotificationPreferenceStore>()));
        builder.Services.Replace(
            ServiceDescriptor.Scoped<INotificationPreferenceWriter>(sp => sp.GetRequiredService<EfCoreNotificationPreferenceStore>()));

        // Subscription store — CQRS forwarding pattern
        builder.Services.RemoveAll<InMemoryNotificationSubscriptionStore>();
        builder.Services.AddScoped<EfCoreNotificationSubscriptionStore>();
        builder.Services.Replace(
            ServiceDescriptor.Scoped<INotificationSubscriptionReader>(sp => sp.GetRequiredService<EfCoreNotificationSubscriptionStore>()));
        builder.Services.Replace(
            ServiceDescriptor.Scoped<INotificationSubscriptionWriter>(sp => sp.GetRequiredService<EfCoreNotificationSubscriptionStore>()));

        // Delivery store — write-only (ISO 27001 audit)
        builder.Services.Replace(
            ServiceDescriptor.Scoped<INotificationDeliveryWriter, EfCoreNotificationDeliveryStore>());

        // MobilePush token store — CQRS forwarding pattern
        builder.Services.RemoveAll<InMemoryMobilePushTokenStore>();
        builder.Services.AddScoped<EfCoreMobilePushTokenStore>();
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IMobilePushTokenReader>(sp => sp.GetRequiredService<EfCoreMobilePushTokenStore>()));
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IMobilePushTokenWriter>(sp => sp.GetRequiredService<EfCoreMobilePushTokenStore>()));

        return builder;
    }
}
