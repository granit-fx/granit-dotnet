using Granit.Invoicing.Commands;
using Wolverine;

namespace Granit.Invoicing.Wolverine.Internal;

/// <summary>
/// Wolverine implementation of <see cref="IInvoiceCommandPublisher"/>.
/// </summary>
internal sealed class WolverineInvoiceCommandPublisher(IMessageBus messageBus) : IInvoiceCommandPublisher
{
    public async Task PublishAsync(CreateInvoiceCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await messageBus.PublishAsync(command).ConfigureAwait(false);
    }
}
