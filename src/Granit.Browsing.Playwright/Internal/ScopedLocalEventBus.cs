using Granit.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Browsing.Playwright.Internal;

/// <summary>
/// Singleton-safe <see cref="ILocalEventBus"/> wrapper. Creates a fresh DI scope per
/// publish, resolves the registered (scoped) bus from it, and disposes the scope —
/// letting <see cref="PlaywrightHeadlessBrowser"/> stay a singleton without capturing
/// a scoped service. A no-op when no <see cref="ILocalEventBus"/> is registered.
/// </summary>
internal sealed class ScopedLocalEventBus(IServiceScopeFactory scopeFactory) : ILocalEventBus
{
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
