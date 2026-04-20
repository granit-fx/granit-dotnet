using Granit.Commands;
using Wolverine;

namespace Granit.Wolverine.Internal;

/// <summary>
/// Wolverine-backed implementation of <see cref="ICommandSender"/> that forwards commands
/// through <see cref="IMessageBus.SendAsync{T}(T, DeliveryOptions?)"/>.
/// </summary>
/// <remarks>
/// Wolverine resolves the single registered handler by message type. The global policies
/// registered in <c>AddGranitWolverine</c> — tenant / user / trace propagation,
/// FluentValidation, retry, DLQ — apply to messages sent through this bus.
/// </remarks>
internal sealed class WolverineCommandSender(IMessageBus bus) : ICommandSender
{
    public async Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : class
    {
        ArgumentNullException.ThrowIfNull(command);
        await bus.SendAsync(command).ConfigureAwait(false);
    }
}
