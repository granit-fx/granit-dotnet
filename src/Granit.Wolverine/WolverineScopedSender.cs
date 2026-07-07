using Granit.MultiTenancy;
using Granit.Wolverine.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Granit.Wolverine;

/// <summary>
/// Sends commands via Wolverine's <see cref="IMessageBus"/> from a Singleton context.
/// </summary>
/// <remarks>
/// <see cref="IMessageBus"/> is Scoped, so Singleton services cannot inject it directly.
/// This helper creates a short-lived scope per dispatch, resolves the bus, and sends.
/// Each dispatch is recorded on the <c>granit.wolverine.messages.dispatched</c> counter,
/// tagged with the tenant active in the created scope (or <c>global</c>).
/// </remarks>
public sealed class WolverineScopedSender(
    IServiceScopeFactory scopeFactory,
    WolverineMetrics metrics)
{
    /// <summary>
    /// Sends a command through a scoped <see cref="IMessageBus"/>.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">Token to cancel the scope creation and dispatch.</param>
    public async Task SendAsync<TCommand>(TCommand command, CancellationToken cancellationToken = default)
        where TCommand : class
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.SendAsync(command).ConfigureAwait(false);

        ICurrentTenant? currentTenant = scope.ServiceProvider.GetService<ICurrentTenant>();
        metrics.RecordMessageDispatched(currentTenant?.Id?.ToString());
    }
}
