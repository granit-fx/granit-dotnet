// =============================================================================
// Tests - NotificationDispatchWorker
// =============================================================================
// Verifies the in-process dispatch worker restores the tenant captured on the
// NotificationTrigger before running fan-out and delivery. Without this, the
// BackgroundService scope defaults to Host context and tenant-scoped delivery
// (recipient contact resolution, per-tenant DB routing) silently resolves
// nothing for tenant users — no email, no delivery attempt.
// =============================================================================

using System.Text.Json;
using System.Threading.Channels;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Diagnostics;
using Granit.Notifications.Domain;
using Granit.Notifications.Handlers;
using Granit.Notifications.Internal;
using Granit.Notifications.Messages;
using Granit.Testing.Fakes;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.Tests;

public sealed class NotificationDispatchWorkerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Dispatch_TenantScopedTrigger_RestoresTenantForDelivery()
    {
        var tenantId = Guid.NewGuid();
        var harness = Harness.Build();
        await using ServiceProvider sp = harness.ServiceProvider;

        await harness.Worker.StartAsync(Ct);
        await harness.Channel.Writer.WriteAsync(
            BuildTrigger(tenantId: tenantId, recipientUserIds: ["user-1"]), Ct);
        await harness.Channel.Sent.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        await harness.Worker.StopAsync(Ct);

        // The Email channel ran (a delivery attempt was produced)...
        harness.Channel.SendCount.ShouldBe(1);
        // ...and it ran inside the originating tenant's ambient context, not Host.
        harness.Channel.CapturedTenantId.ShouldBe(tenantId);

        // The persisted delivery attempt is tagged with the tenant as well.
        await harness.DeliveryWriter.Received(1).TryAcquireDeliveryAttemptAsync(
            Arg.Is<NotificationDeliveryAttempt>(a => a.TenantId == tenantId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dispatch_HostScopedTrigger_RunsInHostContext()
    {
        var harness = Harness.Build();
        await using ServiceProvider sp = harness.ServiceProvider;

        await harness.Worker.StartAsync(Ct);
        await harness.Channel.Writer.WriteAsync(
            BuildTrigger(tenantId: null, recipientUserIds: ["user-1"]), Ct);
        await harness.Channel.Sent.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        await harness.Worker.StopAsync(Ct);

        harness.Channel.SendCount.ShouldBe(1);
        // A Host-scoped notification keeps the default (null) tenant — no regression.
        harness.Channel.CapturedTenantId.ShouldBeNull();
    }

    private static NotificationTrigger BuildTrigger(Guid? tenantId, IReadOnlyList<string> recipientUserIds) => new()
    {
        NotificationTypeName = "test.notification",
        Severity = NotificationSeverity.Warning,
        Data = JsonSerializer.SerializeToElement(new { key = "value" }),
        RecipientUserIds = recipientUserIds,
        TenantId = tenantId,
        OccurredAt = DateTimeOffset.UnixEpoch,
    };

    /// <summary>Wires a real fan-out + delivery pipeline around the worker with substituted leaf services.</summary>
    private sealed record Harness(
        NotificationDispatchWorker Worker,
        ChannelWithSpy Channel,
        INotificationDeliveryWriter DeliveryWriter,
        ServiceProvider ServiceProvider)
    {
        public static Harness Build()
        {
            ChannelWithSpy channel = new();
            FakeCurrentTenant currentTenant = new();
            CapturingEmailChannel emailChannel = new(currentTenant, channel);

            INotificationSubscriptionReader subscriptionReader = Substitute.For<INotificationSubscriptionReader>();
            INotificationPreferenceReader preferenceReader = Substitute.For<INotificationPreferenceReader>();
            preferenceReader.IsChannelEnabledAsync(
                    Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
                .Returns(true);

            INotificationDefinitionStore definitionStore = Substitute.For<INotificationDefinitionStore>();
            definitionStore.Get("test.notification").Returns(
                new NotificationDefinition("test.notification")
                {
                    DefaultChannels = [NotificationChannels.Email],
                    AllowUserOptOut = false,
                });

            INotificationDeliveryWriter deliveryWriter = Substitute.For<INotificationDeliveryWriter>();
            deliveryWriter.HasBeenDeliveredAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);
            deliveryWriter.TryAcquireDeliveryAttemptAsync(
                Arg.Any<NotificationDeliveryAttempt>(), Arg.Any<CancellationToken>()).Returns(true);

            IClock clock = Substitute.For<IClock>();
            clock.Now.Returns(DateTimeOffset.UnixEpoch);

            ServiceCollection services = new();
            services.AddLogging();
            services.AddMetrics();
            services.AddSingleton<NotificationsMetrics>();
            services.AddSingleton(channel.Channel);
            services.AddSingleton<ICurrentTenant>(currentTenant);
            services.AddSingleton<IGuidGenerator, SimpleGuidGenerator>();
            services.AddSingleton(subscriptionReader);
            services.AddSingleton(preferenceReader);
            services.AddSingleton(definitionStore);
            services.AddSingleton(deliveryWriter);
            services.AddSingleton(clock);
            services.AddSingleton<INotificationChannel>(emailChannel);
            services.AddScoped<NotificationFanoutHandler>();
            services.AddScoped<NotificationDeliveryHandler>();

            ServiceProvider sp = services.BuildServiceProvider();

            NotificationDispatchWorker worker = new(
                channel.Channel,
                sp.GetRequiredService<IServiceScopeFactory>(),
                sp.GetRequiredService<ILogger<NotificationDispatchWorker>>());

            return new Harness(worker, channel, deliveryWriter, sp);
        }
    }

    /// <summary>Owns the trigger channel and forwards the Email channel's send signal/capture.</summary>
    private sealed class ChannelWithSpy
    {
        public Channel<NotificationTrigger> Channel { get; } = System.Threading.Channels.Channel.CreateUnbounded<NotificationTrigger>();
        public ChannelWriter<NotificationTrigger> Writer => Channel.Writer;
        private readonly TaskCompletionSource _sent = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Sent => _sent.Task;
        public int SendCount { get; private set; }
        public Guid? CapturedTenantId { get; private set; }

        public void Record(Guid? tenantId)
        {
            CapturedTenantId = tenantId;
            SendCount++;
            _sent.TrySetResult();
        }
    }

    /// <summary>Email channel that captures the ambient tenant at send time (where recipient resolution runs in prod).</summary>
    private sealed class CapturingEmailChannel(ICurrentTenant currentTenant, ChannelWithSpy spy) : INotificationChannel
    {
        public string Name => NotificationChannels.Email;

        public Task SendAsync(NotificationDeliveryContext context, CancellationToken cancellationToken = default)
        {
            spy.Record(currentTenant.Id);
            return Task.CompletedTask;
        }
    }
}
