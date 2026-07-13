using Granit.Encryption;
using Granit.Modularity;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Exceptions;
using Granit.Notifications.Internal;
using Granit.Notifications.Messages;
using Granit.Notifications.Options;
using Granit.Notifications.Wolverine.Internal;
using Granit.Wolverine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wolverine;
using Wolverine.ErrorHandling;

namespace Granit.Notifications.Wolverine;

/// <summary>
/// Granit module that replaces the default Channel-based notification publisher with
/// a durable Wolverine <see cref="IMessageBus"/>-backed implementation.
/// </summary>
/// <remarks>
/// <para>
/// When loaded, Wolverine handles fan-out and delivery via local queues with
/// configurable parallelism and exponential backoff retry.
/// </para>
/// <para>
/// The in-process <see cref="NotificationDispatchWorker"/> remains registered but stays
/// idle because the Wolverine publisher bypasses the channel.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitEncryptionModule),
    typeof(GranitNotificationsModule),
    typeof(GranitWolverineModule))]
public sealed class GranitNotificationsWolverineModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Replace the default Channel-based publisher with Wolverine IMessageBus.
        context.Services.Replace(ServiceDescriptor
            .Scoped<INotificationPublisher, WolverineNotificationPublisher>());

        // Resolve options for queue configuration.
        NotificationsOptions options = new();
        context.Configuration
            .GetSection(NotificationsOptions.SectionName)
            .Bind(options);

        // Configure Wolverine local queues and retry policies.
        context.Services.ConfigureWolverine(opts =>
        {
            opts.LocalQueueFor<NotificationTrigger>()
                .Named("notification-fanout");

            opts.LocalQueueFor<DeliverNotificationCommand>()
                .Named("notification-delivery")
                .MaximumParallelMessages(options.MaxParallelDeliveries);

            opts.OnException<NotificationDeliveryException>()
                .RetryWithCooldown(
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromMinutes(1),
                    TimeSpan.FromMinutes(5),
                    TimeSpan.FromMinutes(30),
                    TimeSpan.FromHours(2));
        });
    }
}
