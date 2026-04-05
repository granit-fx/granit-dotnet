using Granit.Payments.Commands;

namespace Granit.Payments;

/// <summary>
/// Processes inbound webhook events from payment providers.
/// </summary>
public interface IWebhookProcessor
{
    /// <summary>
    /// Reconciles the provider-reported status with the local payment transaction
    /// based on an inbound webhook event.
    /// </summary>
    Task ProcessAsync(ProcessWebhookCommand command, CancellationToken cancellationToken);
}
