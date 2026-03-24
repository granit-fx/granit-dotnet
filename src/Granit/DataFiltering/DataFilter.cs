using System.Collections.Immutable;

namespace Granit.DataFiltering;

/// <summary>
/// Implementation of <see cref="IDataFilter"/> using <see cref="AsyncLocal{T}"/>.
/// Thread-safe: each async flow has its own independent filter state.
/// Default state: all filters enabled (empty dictionary = no overrides active).
/// </summary>
/// <remarks>
/// The state is stored in a <c>static readonly AsyncLocal</c> field, not in instance
/// fields. Registering as Singleton is correct and intentional — identical to
/// <c>CurrentTenant</c> which also uses a static AsyncLocal.
/// <para>
/// Each state mutation creates a new <see cref="ImmutableDictionary{TKey,TValue}"/> via
/// <c>SetItem</c> (copy-on-write). The previous dictionary is captured for restoration.
/// This avoids the AsyncLocal child-flow mutation trap.
/// </para>
/// </remarks>
public sealed class DataFilter : IDataFilter
{
    // Absence of a Type key in the dictionary means the filter is enabled (default).
    // Never mutate in place — always replace the AsyncLocal value with SetItem.
    private static readonly AsyncLocal<ImmutableDictionary<Type, bool>> _state = new();

    private static ImmutableDictionary<Type, bool> CurrentState =>
        _state.Value ?? ImmutableDictionary<Type, bool>.Empty;

    /// <inheritdoc/>
    public IDisposable Disable<TFilter>() where TFilter : class =>
        SetState<TFilter>(enabled: false);

    /// <inheritdoc/>
    public IDisposable Enable<TFilter>() where TFilter : class =>
        SetState<TFilter>(enabled: true);

    /// <inheritdoc/>
    public bool IsEnabled<TFilter>() where TFilter : class =>
        !CurrentState.TryGetValue(typeof(TFilter), out bool value) || value;

    private static FilterScope SetState<TFilter>(bool enabled) where TFilter : class
    {
        ImmutableDictionary<Type, bool> previous = CurrentState;
        _state.Value = previous.SetItem(typeof(TFilter), enabled);
        return new FilterScope(previous);
    }

    private sealed class FilterScope(ImmutableDictionary<Type, bool> previous) : IDisposable
    {
        private readonly ImmutableDictionary<Type, bool> _previous = previous;
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                _state.Value = _previous;
            }
        }
    }
}
