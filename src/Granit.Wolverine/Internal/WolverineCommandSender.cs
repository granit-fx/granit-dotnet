using Granit.Commands;
using Granit.MultiTenancy;
using Granit.Wolverine.Diagnostics;
using Wolverine;

namespace Granit.Wolverine.Internal;

/// <summary>
/// Wolverine-backed implementation of <see cref="ICommandSender"/> that forwards commands
/// through <see cref="IMessageBus.SendAsync{T}(T, DeliveryOptions?)"/>.
/// </summary>
/// <remarks>
/// <para>
/// Registered as <b>Scoped</b> so the enclosing scope's <c>ICurrentTenant</c> /
/// <c>ICurrentUserService</c> / W3C trace context flow into the
/// <c>OutgoingContextMiddleware</c> and end up as <c>X-Tenant-Id</c> / <c>X-User-Id</c> /
/// <c>traceparent</c> headers on the outgoing envelope.
/// </para>
/// <para>
/// Singleton consumers (<c>IHostedService</c>) that need to dispatch commands must create
/// a DI scope themselves via <c>IServiceScopeFactory.CreateAsyncScope()</c> and resolve
/// <see cref="ICommandSender"/> inside it.
/// </para>
/// <para>
/// Each dispatch is recorded on the <c>granit.wolverine.messages.dispatched</c> counter,
/// tagged with the current tenant (or <c>global</c>).
/// </para>
/// </remarks>
internal sealed class WolverineCommandSender(
    IMessageBus bus,
    WolverineMetrics metrics,
    ICurrentTenant currentTenant) : ICommandSender
{
    public async Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : class
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        await bus.SendAsync(command).ConfigureAwait(false);
        metrics.RecordMessageDispatched(currentTenant.Id?.ToString());
    }
}
