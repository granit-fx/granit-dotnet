// =============================================================================
// Tests - NotificationDeliveryHandler concurrency (SQLite)
// =============================================================================
// GH #947 — parallel claims for the same delivery id must dedupe SMTP sends.
// =============================================================================

using System.Diagnostics.Metrics;
using System.Text.Json;
using System.Threading;
using Granit.Domain;
using Granit.Guids;
using Granit.Notifications.Abstractions;
using Granit.Notifications.Diagnostics;
using Granit.Notifications.Domain;
using Granit.Notifications.EntityFrameworkCore.Internal;
using Granit.Notifications.Handlers;
using Granit.Notifications.Messages;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Notifications.EntityFrameworkCore.Tests;

public sealed class NotificationDeliveryHandlerConcurrencyTests : IDisposable
{
    private readonly TestDbContextFactory _factory = TestDbContextFactory.Create();
    private readonly EfCoreNotificationDeliveryStore _store;
    private readonly ServiceProvider _meterProvider;
    private readonly NotificationsMetrics _metrics;
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly CountingEmailChannel _channel = new();

    public NotificationDeliveryHandlerConcurrencyTests()
    {
        _clock.Now.Returns(_ => DateTimeOffset.UtcNow);
        _store = new EfCoreNotificationDeliveryStore(_factory, _clock);

        ServiceCollection svc = new();
        svc.AddMetrics();
        _meterProvider = svc.BuildServiceProvider();
        IMeterFactory meters = _meterProvider.GetRequiredService<IMeterFactory>();
        _metrics = new NotificationsMetrics(meters);
    }

    public void Dispose()
    {
        _meterProvider.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Parallel_same_delivery_parallel_handlers_sends_email_once()
    {
        DeliverNotificationCommand command = BuildCommand();

        NotificationDeliveryHandler handlerOne = BuildHandler(_channel);
        NotificationDeliveryHandler handlerTwo = BuildHandler(_channel);

        await Task.WhenAll(
            handlerOne.HandleAsync(command, TestContext.Current.CancellationToken),
            handlerTwo.HandleAsync(command, TestContext.Current.CancellationToken));

        _channel.SendCount.ShouldBe(1);
        (await _store.HasBeenDeliveredAsync(command.DeliveryId, TestContext.Current.CancellationToken)).ShouldBeTrue();

        await using NotificationsDbContext db = _factory.CreateDbContext();
        (await db.DeliveryAttempts.AsNoTracking()
            .CountAsync(a => a.DeliveryId == command.DeliveryId, TestContext.Current.CancellationToken))
            .ShouldBe(1);
    }

    private NotificationDeliveryHandler BuildHandler(INotificationChannel channel) =>
        new([channel], _store, new SimpleGuidGenerator(), _clock, NullLogger<NotificationDeliveryHandler>.Instance, _metrics);

    private static DeliverNotificationCommand BuildCommand()
    {
        var deliveryId = Guid.NewGuid();
        return new DeliverNotificationCommand
        {
            DeliveryId = deliveryId,
            NotificationId = Guid.NewGuid(),
            NotificationTypeName = "test.notification",
            Severity = NotificationSeverity.Info,
            RecipientUserId = "user-1",
            ChannelName = NotificationChannels.Email,
            Data = JsonSerializer.SerializeToElement(new { key = "value" }),
            OccurredAt = DateTimeOffset.UtcNow,
            TenantId = Guid.NewGuid(),
        };
    }

    private sealed class CountingEmailChannel : INotificationChannel
    {
        private int _sendCount;

        internal int SendCount => Volatile.Read(ref _sendCount);

        public string Name => NotificationChannels.Email;

        public Task SendAsync(NotificationDeliveryContext context, CancellationToken cancellationToken = default)
        {
            _ = Interlocked.Increment(ref _sendCount);
            return Task.CompletedTask;
        }
    }
}
