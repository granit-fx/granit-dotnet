using Granit.DataExchange;
using Granit.Encryption;
using Granit.Guids;
using Granit.Localization;
using Granit.Modularity;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Diagnostics;
using Granit.Notifications.Extensions;
using Granit.Notifications.Internal;
using Granit.QueryEngine;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Granit.Notifications;

/// <summary>
/// Granit module for the multi-channel notification engine.
/// </summary>
/// <remarks>
/// Default registrations use in-memory stores and in-process channel dispatch,
/// suitable for development and tests. For production, add
/// <c>Granit.Notifications.Wolverine</c> for durable outbox dispatch and call
/// <c>AddGranitNotificationsEntityFrameworkCore()</c> for persistent stores.
/// </remarks>
[DependsOn(
    typeof(GranitDataExchangeAbstractionsModule),
    typeof(GranitEncryptionModule),
    typeof(GranitGuidsModule),
    typeof(GranitLocalizationModule),
    typeof(GranitNotificationsAbstractionsModule),
    typeof(GranitQueryEngineAbstractionsModule),
    typeof(GranitTimingModule))]
public sealed class GranitNotificationsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Builder.AddGranitNotifications();

    /// <inheritdoc/>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        IHostEnvironment environment = context.ServiceProvider.GetRequiredService<IHostEnvironment>();

        if (!environment.IsDevelopment() &&
            context.ServiceProvider.GetRequiredService<IUserNotificationReader>() is InMemoryUserNotificationStore)
        {
            NotificationsLog.InMemoryStoresActiveInNonDevelopment(
                context.ServiceProvider.GetRequiredService<ILogger<GranitNotificationsModule>>());
        }
    }
}
