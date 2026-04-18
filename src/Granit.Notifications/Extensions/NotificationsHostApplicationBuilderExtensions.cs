using System.Threading.Channels;
using Granit.DataExchange.Extensions;
using Granit.Diagnostics;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Diagnostics;
using Granit.Notifications.Domain;
using Granit.Notifications.Exports;
using Granit.Notifications.Handlers;
using Granit.Notifications.Internal;
using Granit.Notifications.Messages;
using Granit.Notifications.Options;
using Granit.Notifications.Queries;
using Granit.QueryEngine.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Notifications.Extensions;

/// <summary>
/// Extension methods for registering Granit.Notifications services.
/// </summary>
public static class NotificationsHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the Granit notification dispatch engine.
    /// </summary>
    /// <remarks>
    /// By default, notifications are dispatched via an in-process <see cref="Channel{T}"/>
    /// consumed by a <see cref="BackgroundService"/>. For durable outbox-backed dispatch,
    /// add the <c>Granit.Notifications.Wolverine</c> package.
    /// </remarks>
    public static IHostApplicationBuilder AddGranitNotifications(
        this IHostApplicationBuilder builder,
        Action<NotificationsOptions>? configure = null)
    {
        GranitActivitySourceRegistry.Register(Diagnostics.NotificationsActivitySource.Name);
        builder.Services.TryAddSingleton<NotificationsMetrics>();

        // Options
        builder.Services
            .AddOptions<NotificationsOptions>()
            .BindConfiguration(NotificationsOptions.SectionName)
            .ValidateOnStart();

        NotificationsOptions options = new();
        builder.Configuration.GetSection(NotificationsOptions.SectionName).Bind(options);
        configure?.Invoke(options);

        // Default (replaceable) store registrations — CQRS forwarding pattern
        builder.Services.AddSingleton<InMemoryUserNotificationStore>();
        builder.Services.AddSingleton<IUserNotificationReader>(sp => sp.GetRequiredService<InMemoryUserNotificationStore>());
        builder.Services.AddSingleton<IUserNotificationWriter>(sp => sp.GetRequiredService<InMemoryUserNotificationStore>());

        builder.Services.AddSingleton<InMemoryNotificationPreferenceStore>();
        builder.Services.AddSingleton<INotificationPreferenceReader>(sp => sp.GetRequiredService<InMemoryNotificationPreferenceStore>());
        builder.Services.AddSingleton<INotificationPreferenceWriter>(sp => sp.GetRequiredService<InMemoryNotificationPreferenceStore>());

        builder.Services.AddSingleton<InMemoryNotificationSubscriptionStore>();
        builder.Services.AddSingleton<INotificationSubscriptionReader>(sp => sp.GetRequiredService<InMemoryNotificationSubscriptionStore>());
        builder.Services.AddSingleton<INotificationSubscriptionWriter>(sp => sp.GetRequiredService<InMemoryNotificationSubscriptionStore>());

        builder.Services.AddScoped<INotificationDeliveryWriter, NullNotificationDeliveryWriter>();

        // Definition store (singleton) — initialized eagerly from all registered providers
        builder.Services.AddSingleton<NotificationDefinitionStore>(sp =>
        {
            NotificationDefinitionStore store = new();
            IEnumerable<INotificationDefinitionProvider> providers = sp.GetServices<INotificationDefinitionProvider>();
            NotificationDefinitionContext ctx = new();
            foreach (INotificationDefinitionProvider provider in providers)
            {
                provider.Define(ctx);
            }

            store.Initialize(ctx.GetDefinitions());
            return store;
        });
        builder.Services.AddSingleton<INotificationDefinitionStore>(sp => sp.GetRequiredService<NotificationDefinitionStore>());

        // Handlers (scoped — required by the Channel-based worker)
        builder.Services.AddScoped<NotificationFanoutHandler>();
        builder.Services.AddScoped<NotificationDeliveryHandler>();

        // In-process channel dispatch (default — replaced by Granit.Notifications.Wolverine)
        builder.Services.AddSingleton(Channel.CreateUnbounded<NotificationTrigger>());
        builder.Services.AddScoped<INotificationPublisher, ChannelNotificationPublisher>();
        builder.Services.AddHostedService<NotificationDispatchWorker>();

        // InApp channel (built-in) — Scoped because IUserNotificationWriter is Scoped when
        // the EF Core provider is active (captive dependency if registered as Singleton).
        builder.Services.AddScoped<INotificationChannel, InAppNotificationChannel>();

        // Query + Export definitions (ADR-020: owned by the base module).
        builder.Services.AddQueryDefinition<UserNotification, UserNotificationQueryDefinition>();
        builder.Services.AddQueryDefinition<NotificationPreference, NotificationPreferenceQueryDefinition>();
        builder.Services.AddExportDefinition<UserNotification, UserNotificationExportDefinition>();
        builder.Services.AddExportDefinition<NotificationPreference, NotificationPreferenceExportDefinition>();

        return builder;
    }

    /// <summary>
    /// Registers a notification definition provider.
    /// </summary>
    public static IServiceCollection AddNotificationDefinitions<TProvider>(this IServiceCollection services)
        where TProvider : class, INotificationDefinitionProvider
    {
        services.AddSingleton<INotificationDefinitionProvider, TProvider>();
        return services;
    }
}
