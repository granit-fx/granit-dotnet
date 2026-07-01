using Granit.Notifications.MobilePush.EntityFrameworkCore.Internal;
using Granit.Notifications.MobilePush.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Notifications.MobilePush.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for enabling EF Core persistence of the Granit mobile push channel.
/// </summary>
public static class MobilePushEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Replaces the default in-memory mobile push token store with a durable EF Core
    /// implementation backed by an isolated <see cref="MobilePushDbContext"/>.
    /// </summary>
    /// <remarks>
    /// Must be called after <c>AddGranitNotificationsMobilePush()</c>. Registers
    /// <see cref="EfCoreMobilePushTokenStore"/> in place of the in-memory token store and
    /// wires the <see cref="MobilePushDbContext"/> via <c>IDbContextFactory</c> for thread-safe
    /// use in Wolverine handlers.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core <see cref="DbContextOptionsBuilder"/> configuration (provider + connection string).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitNotificationsMobilePushEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        builder.Services.AddDbContextFactory<MobilePushDbContext>((sp, options) =>
        {
            configure(options);
            options.UseGranitInterceptors(sp);
        }, ServiceLifetime.Scoped);

        // MobilePush token store — CQRS forwarding pattern.
        // Scoped: AddDbContextFactory registers IDbContextFactory<T> as Scoped (interceptors
        // depend on ICurrentTenant/ICurrentUser which are Scoped). Stores injecting the factory
        // must also be Scoped to avoid captive dependency violations.
        builder.Services.RemoveAll<InMemoryMobilePushTokenStore>();
        builder.Services.AddScoped<EfCoreMobilePushTokenStore>();
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IMobilePushTokenReader>(sp => sp.GetRequiredService<EfCoreMobilePushTokenStore>()));
        builder.Services.Replace(
            ServiceDescriptor.Scoped<IMobilePushTokenWriter>(sp => sp.GetRequiredService<EfCoreMobilePushTokenStore>()));

        return builder;
    }
}
