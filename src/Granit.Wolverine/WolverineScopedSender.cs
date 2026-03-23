using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Granit.Wolverine;

/// <summary>
/// Sends commands via Wolverine's <see cref="IMessageBus"/> from a Singleton context.
/// </summary>
/// <remarks>
/// <see cref="IMessageBus"/> is Scoped, so Singleton services cannot inject it directly.
/// This helper creates a short-lived scope per dispatch, resolves the bus, and sends.
/// </remarks>
public sealed class WolverineScopedSender(IServiceScopeFactory scopeFactory)
{
    /// <summary>
    /// Sends a command through a scoped <see cref="IMessageBus"/>.
    /// </summary>
    /// <typeparam name="TCommand">The command type.</typeparam>
    /// <param name="command">The command to send.</param>
    public async Task SendAsync<TCommand>(TCommand command) where TCommand : class
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IMessageBus bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
        await bus.SendAsync(command).ConfigureAwait(false);
    }
}
