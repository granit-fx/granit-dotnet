using Granit.Payments.Commands;
using Wolverine;

namespace Granit.Payments.Wolverine.Internal;

/// <summary>
/// Wolverine implementation of <see cref="IPaymentCommandDispatcher"/>.
/// </summary>
internal sealed class WolverinePaymentCommandDispatcher(IMessageBus messageBus) : IPaymentCommandDispatcher
{
    public async Task SendAsync(InitiatePaymentCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await messageBus.SendAsync(command).ConfigureAwait(false);
    }
}
