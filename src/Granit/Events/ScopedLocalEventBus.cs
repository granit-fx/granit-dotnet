using Microsoft.Extensions.DependencyInjection;

namespace Granit.Events;

/// <summary>
/// Singleton-safe <see cref="ILocalEventBus"/> wrapper for callers that must publish
/// local events from a singleton context (hosted services, pool owners, capability
/// implementations, …).
/// </summary>
/// <remarks>
/// <para>
/// The default <see cref="ILocalEventBus"/> registrations (<c>InProcessLocalEventBus</c>
/// in <c>Granit.Events</c> and <c>WolverineLocalEventBus</c> in <c>Granit.Events.Wolverine</c>)
/// are <c>Scoped</c> because they enlist with the ambient request/unit-of-work. Capturing
/// the bus directly in a singleton's constructor is a captive dependency: it fails
/// <c>ValidateScopes</c> (default in Development) and, when validation is off, leaks the
/// first request's scope across every subsequent publish.
/// </para>
/// <para>
/// This wrapper takes an <see cref="IServiceScopeFactory"/> (singleton) and creates a
/// fresh DI scope per <see cref="PublishAsync"/> call, resolving and dispatching through
/// the real bus. Events published this way do <em>not</em> enlist with any ambient
/// unit-of-work in the caller's scope — appropriate for telemetry-style events emitted
/// by infrastructure singletons. For per-request, transactionally-enlisted publishes,
/// resolve the bus from the request scope directly.
/// </para>
/// <para>
/// Construction is gated by <see cref="TryCreate(IServiceScopeFactory?)"/>: when no
/// <see cref="ILocalEventBus"/> is registered the factory returns <c>null</c>, so callers
/// can preserve the conventional "no bus registered → no-op publish" semantic via a
/// simple null-check.
/// </para>
/// </remarks>
public sealed class ScopedLocalEventBus(IServiceScopeFactory scopeFactory) : ILocalEventBus
{
    /// <summary>
    /// Returns a wrapper bound to <paramref name="scopeFactory"/>, or <c>null</c> when
    /// <see cref="ILocalEventBus"/> is not registered in the host.
    /// </summary>
    public static ScopedLocalEventBus? TryCreate(IServiceScopeFactory? scopeFactory)
    {
        if (scopeFactory is null)
        {
            return null;
        }
        using IServiceScope probe = scopeFactory.CreateScope();
        if (probe.ServiceProvider.GetService<ILocalEventBus>() is null)
        {
            return null;
        }
        return new ScopedLocalEventBus(scopeFactory);
    }

    /// <inheritdoc/>
    public async Task PublishAsync<TEvent>(TEvent localEvent, CancellationToken cancellationToken = default)
        where TEvent : class
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        ILocalEventBus? inner = scope.ServiceProvider.GetService<ILocalEventBus>();
        if (inner is null)
        {
            return;
        }
        await inner.PublishAsync(localEvent, cancellationToken).ConfigureAwait(false);
    }
}
