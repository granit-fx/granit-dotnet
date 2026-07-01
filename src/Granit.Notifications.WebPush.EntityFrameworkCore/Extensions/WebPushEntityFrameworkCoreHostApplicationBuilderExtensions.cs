using Granit.Notifications.WebPush.EntityFrameworkCore.Internal;
using Granit.Notifications.WebPush.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Notifications.WebPush.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for enabling EF Core persistence of the Granit Web Push channel.
/// </summary>
public static class WebPushEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Replaces the default in-memory browser push subscription store with a durable EF Core
    /// implementation backed by an isolated <see cref="WebPushDbContext"/>, so subscriptions
    /// survive restarts.
    /// </summary>
    /// <remarks>
    /// Must be called after <c>AddGranitNotificationsWebPush()</c>. Registers
    /// <see cref="EfCoreWebPushSubscriptionStore"/> in place of the in-memory store and wires the
    /// <see cref="WebPushDbContext"/> via <c>IDbContextFactory</c> for thread-safe use in
    /// Wolverine handlers.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core <see cref="DbContextOptionsBuilder"/> configuration (provider + connection string).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitNotificationsWebPushEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddDbContextFactory<WebPushDbContext>((sp, options) =>
        {
            configure(options);
            options.UseGranitInterceptors(sp);
        }, ServiceLifetime.Scoped);

        // Browser push subscription store — CQRS forwarding pattern.
        // Scoped: AddDbContextFactory registers IDbContextFactory<T> as Scoped (interceptors
        // depend on ICurrentTenant/ICurrentUser which are Scoped). Stores injecting the factory
        // must also be Scoped to avoid captive dependency violations.
        builder.Services.RemoveAll<InMemoryWebPushSubscriptionStore>();
        builder.Services.AddScoped<EfCoreWebPushSubscriptionStore>();
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IWebPushSubscriptionReader>(sp => sp.GetRequiredService<EfCoreWebPushSubscriptionStore>()));
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IWebPushSubscriptionWriter>(sp => sp.GetRequiredService<EfCoreWebPushSubscriptionStore>()));

        return builder;
    }
}
