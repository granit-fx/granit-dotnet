using System.Text.Json;
using Granit.Core.MultiTenancy;
using Granit.Timing;
using Granit.Webhooks.Messages;
using Granit.Webhooks.Wolverine.Internal;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Webhooks.Wolverine.Tests;

public sealed class WolverineWebhookPublisherTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();
    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
    private readonly IClock _clock = Substitute.For<IClock>();

    private static readonly DateTimeOffset _now = new(2026, 3, 18, 12, 0, 0, TimeSpan.Zero);

    public WolverineWebhookPublisherTests()
    {
        _tenant.IsAvailable.Returns(false);
        _clock.Now.Returns(_now);
    }

    [Fact]
    public async Task PublishAsync_PublishesWebhookTrigger()
    {
        WolverineWebhookPublisher sut = new(_bus, _tenant, _clock);

        await sut.PublishAsync("order.created", new { OrderId = 42 }, TestContext.Current.CancellationToken);

        await _bus.Received(1).PublishAsync(Arg.Is<WebhookTrigger>(t =>
            t.EventType == "order.created" &&
            t.OccurredAt == _now &&
            t.TenantId == null));
    }

    [Fact]
    public async Task PublishAsync_WithActiveTenant_SetsTenantId()
    {
        var tenantId = Guid.NewGuid();
        _tenant.IsAvailable.Returns(true);
        _tenant.Id.Returns(tenantId);
        WolverineWebhookPublisher sut = new(_bus, _tenant, _clock);

        await sut.PublishAsync("order.created", new { }, TestContext.Current.CancellationToken);

        await _bus.Received(1).PublishAsync(Arg.Is<WebhookTrigger>(t => t.TenantId == tenantId));
    }

    [Fact]
    public async Task PublishAsync_PayloadIsSerializedToJsonElement()
    {
        WolverineWebhookPublisher sut = new(_bus, _tenant, _clock);
        var payload = new { Name = "test", Value = 99 }; // anonymous type — var required

        await sut.PublishAsync("item.updated", payload, TestContext.Current.CancellationToken);

        await _bus.Received(1).PublishAsync(Arg.Is<WebhookTrigger>(t =>
            t.Payload.ValueKind == JsonValueKind.Object));
    }
}
