using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Messages;
using Granit.Webhooks.Wolverine.Internal;
using NSubstitute;
using Shouldly;
using Wolverine;
using Xunit;

namespace Granit.Webhooks.Wolverine.Tests;

public sealed class WolverineWebhookCommandDispatcherTests
{
    private readonly IMessageBus _bus = Substitute.For<IMessageBus>();

    [Fact]
    public async Task DispatchAsync_DelegatesCommandToMessageBus()
    {
        WolverineWebhookCommandDispatcher sut = new(_bus);
        SendWebhookCommand command = new()
        {
            DeliveryId = Guid.NewGuid(),
            SubscriptionId = Guid.NewGuid(),
            TargetUrl = "https://example.com/webhook",
            SigningSecret = "secret",
            Envelope = new()
            {
                EventId = Guid.NewGuid(),
                EventType = "test.event",
                TenantId = null,
                Timestamp = DateTimeOffset.UtcNow,
                ApiVersion = "2026-01-01",
                Data = System.Text.Json.JsonSerializer.SerializeToElement(new { }),
            },
        };

        await sut.DispatchAsync(command, TestContext.Current.CancellationToken);

        await _bus.Received(1).SendAsync(command);
    }

    [Fact]
    public void WolverineWebhookCommandDispatcher_ImplementsIWebhookCommandDispatcher() =>
        typeof(WolverineWebhookCommandDispatcher).IsAssignableTo(typeof(IWebhookCommandDispatcher)).ShouldBeTrue();
}
