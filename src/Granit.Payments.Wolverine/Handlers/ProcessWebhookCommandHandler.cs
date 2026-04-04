using Granit.Payments.Commands;
using Granit.Payments.Wolverine.Services;

namespace Granit.Payments.Wolverine.Handlers;

/// <summary>
/// Processes inbound webhook events from payment providers.
/// Delegates to <see cref="WebhookProcessor"/> for all reconciliation logic.
/// </summary>
public class ProcessWebhookCommandHandler
{
    public static Task HandleAsync(
        ProcessWebhookCommand command,
        WebhookProcessor processor,
        CancellationToken cancellationToken) =>
        processor.ProcessAsync(command, cancellationToken);
}
