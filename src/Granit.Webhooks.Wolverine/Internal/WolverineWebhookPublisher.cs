using System.Text.Json;
using Granit.MultiTenancy;
using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Messages;
using Wolverine;

namespace Granit.Webhooks.Wolverine.Internal;

/// <summary>
/// <see cref="IWebhookPublisher"/> implementation backed by Wolverine's durable Outbox.
/// </summary>
internal sealed class WolverineWebhookPublisher(
    IMessageBus bus,
    ICurrentTenant currentTenant,
    IClock clock) : IWebhookPublisher
{
    public async ValueTask PublishAsync<TPayload>(
        string eventType,
        TPayload payload,
        CancellationToken cancellationToken = default) where TPayload : notnull
    {
        JsonElement serializedPayload = JsonSerializer.SerializeToElement(payload);

        WebhookTrigger trigger = new()
        {
            EventType = eventType,
            Payload = serializedPayload,
            TenantId = currentTenant.IsAvailable ? currentTenant.Id : null,
            OccurredAt = clock.Now,
        };

        await bus.PublishAsync(trigger).ConfigureAwait(false);
    }
}
