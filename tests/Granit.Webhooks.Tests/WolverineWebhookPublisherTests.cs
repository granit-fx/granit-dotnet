// =============================================================================
// Tests - ChannelWebhookPublisher
// =============================================================================
// Verifies that PublishAsync serializes the payload and writes a WebhookTrigger
// to the in-process channel with correct fields, including tenant context handling.
// =============================================================================

using System.Text.Json;
using System.Threading.Channels;
using Granit.MultiTenancy;
using Granit.Timing;
using Granit.Webhooks.Internal;
using Granit.Webhooks.Messages;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Webhooks.Tests;

public sealed class ChannelWebhookPublisherTests
{
    private readonly Channel<WebhookTrigger> _channel = Channel.CreateUnbounded<WebhookTrigger>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly DateTimeOffset _fixedNow = new(2025, 6, 15, 10, 0, 0, TimeSpan.Zero);

    public ChannelWebhookPublisherTests()
    {
        _clock.Now.Returns(_ => _fixedNow);
    }

    [Fact]
    public async Task PublishAsync_WithTenant_PublishesTriggerWithTenantId()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);

        var publisher = new ChannelWebhookPublisher(_channel, _currentTenant, _clock);
        var payload = new { DocumentId = Guid.NewGuid(), Name = "test.pdf" };

        await publisher.PublishAsync("document.uploaded", payload, TestContext.Current.CancellationToken);

        _channel.Reader.TryRead(out WebhookTrigger? trigger).ShouldBeTrue();
        trigger!.EventType.ShouldBe("document.uploaded");
        trigger.TenantId.ShouldBe(tenantId);
        trigger.OccurredAt.ShouldBe(_fixedNow);
    }

    [Fact]
    public async Task PublishAsync_WithoutTenant_PublishesTriggerWithNullTenantId()
    {
        _currentTenant.IsAvailable.Returns(false);

        var publisher = new ChannelWebhookPublisher(_channel, _currentTenant, _clock);

        await publisher.PublishAsync("test.event", new { Key = "value" }, TestContext.Current.CancellationToken);

        _channel.Reader.TryRead(out WebhookTrigger? trigger).ShouldBeTrue();
        trigger!.TenantId.ShouldBeNull();
    }

    [Fact]
    public async Task PublishAsync_SerializesPayloadToJsonElement()
    {
        _currentTenant.IsAvailable.Returns(false);

        var publisher = new ChannelWebhookPublisher(_channel, _currentTenant, _clock);
        var payload = new { Id = 42, Label = "test" };

        await publisher.PublishAsync("item.created", payload, TestContext.Current.CancellationToken);

        _channel.Reader.TryRead(out WebhookTrigger? trigger).ShouldBeTrue();
        trigger!.Payload.GetProperty("Id").GetInt32().ShouldBe(42);
        trigger.Payload.GetProperty("Label").GetString().ShouldBe("test");
    }

    [Fact]
    public async Task PublishAsync_SetsOccurredAtFromClock()
    {
        _currentTenant.IsAvailable.Returns(false);

        var publisher = new ChannelWebhookPublisher(_channel, _currentTenant, _clock);

        await publisher.PublishAsync("test.event", new { }, TestContext.Current.CancellationToken);

        _channel.Reader.TryRead(out WebhookTrigger? trigger).ShouldBeTrue();
        trigger!.OccurredAt.ShouldBe(_fixedNow);
    }
}
