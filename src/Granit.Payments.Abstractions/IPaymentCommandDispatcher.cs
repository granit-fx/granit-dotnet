using Granit.Payments.Commands;

namespace Granit.Payments;

/// <summary>
/// Dispatches payment commands to the messaging infrastructure.
/// </summary>
public interface IPaymentCommandDispatcher
{
    /// <summary>
    /// Sends an <see cref="InitiatePaymentCommand"/> for processing.
    /// </summary>
    Task SendAsync(InitiatePaymentCommand command, CancellationToken cancellationToken = default);
}
