using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Granit.Browsing.Internal;

/// <summary>
/// Minimal thread-safe <see cref="IObservable{T}"/> implementation used by providers to
/// surface console messages / page errors without taking a dependency on Reactive
/// Extensions. Subscriber exceptions are logged (or swallowed when no logger is wired)
/// and never propagate to other subscribers or the producer.
/// </summary>
internal sealed class SimpleObservable<T> : IObservable<T>
{
    private ImmutableList<IObserver<T>> _observers = ImmutableList<IObserver<T>>.Empty;
    private readonly ILogger? _logger;

    public SimpleObservable(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(IObserver<T> observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        ImmutableInterlocked.Update(ref _observers, static (list, o) => list.Add(o), observer);
        return new Subscription(this, observer);
    }

    /// <summary>Publishes <paramref name="value"/> to every current subscriber.</summary>
    /// <remarks>
    /// A subscriber that throws is logged (or swallowed) — other subscribers still receive
    /// the value and the producer is not interrupted.
    /// </remarks>
    public void Publish(T value)
    {
        // Snapshot under volatile-read semantics (ImmutableList is immutable).
        ImmutableList<IObserver<T>> snapshot = _observers;
        foreach (IObserver<T> observer in snapshot)
        {
            try
            {
                observer.OnNext(value);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "Subscriber threw while handling observable value.");
            }
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly SimpleObservable<T> _parent;
        private IObserver<T>? _observer;

        public Subscription(SimpleObservable<T> parent, IObserver<T> observer)
        {
            _parent = parent;
            _observer = observer;
        }

        public void Dispose()
        {
            IObserver<T>? observer = Interlocked.Exchange(ref _observer, null);
            if (observer is null)
            {
                return;
            }

            ImmutableInterlocked.Update(
                ref _parent._observers,
                static (list, o) => list.Remove(o),
                observer);
        }
    }
}
