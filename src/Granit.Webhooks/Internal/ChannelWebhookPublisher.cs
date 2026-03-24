using System.Text.Json;
using System.Threading.Channels;
using Granit.MultiTenancy;
using Granit.Timing;
using Granit.Webhooks.Abstractions;
using Granit.Webhooks.Messages;

namespace Granit.Webhooks.Internal;

/// <summary>
/// Default <see cref="IWebhookPublisher"/> implementation that writes
/// <see cref="WebhookTrigger"/> messages to an in-process channel consumed
/// by <see cref="WebhookDispatchWorker"/>.
/// </summary>
internal sealed class ChannelWebhookPublisher(
    Channel<WebhookTrigger> channel,
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

        await channel.Writer.WriteAsync(trigger, cancellationToken).ConfigureAwait(false);
    }
}
