using Granit.Payments.Commands;

namespace Granit.Payments.Wolverine.Handlers;

/// <summary>
/// Processes inbound webhook events from payment providers.
/// Delegates to <see cref="IWebhookProcessor"/> for all reconciliation logic.
/// </summary>
public class ProcessWebhookCommandHandler
{
    public static Task HandleAsync(
        ProcessWebhookCommand command,
        IWebhookProcessor processor,
        CancellationToken cancellationToken) =>
        processor.ProcessAsync(command, cancellationToken);
}
