using Granit.Invoicing.Commands;

namespace Granit.Invoicing;

/// <summary>
/// Publishes invoice commands to the messaging infrastructure.
/// </summary>
public interface IInvoiceCommandPublisher
{
    /// <summary>
    /// Publishes a <see cref="CreateInvoiceCommand"/> for asynchronous processing.
    /// </summary>
    Task PublishAsync(CreateInvoiceCommand command, CancellationToken cancellationToken = default);
}
