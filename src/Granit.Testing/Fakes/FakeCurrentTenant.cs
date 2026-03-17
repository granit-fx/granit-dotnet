using Granit.Core.MultiTenancy;

namespace Granit.Testing.Fakes;

/// <summary>
/// Configurable fake implementation of <see cref="ICurrentTenant"/> for tests.
/// </summary>
/// <remarks>
/// <para>
/// All state is stored in <see cref="AsyncLocal{T}"/> so that each async context
/// (i.e. each xUnit test) gets isolated values — safe for parallel execution
/// and <c>IClassFixture&lt;T&gt;</c> sharing.
/// </para>
/// <para>
/// <see cref="Change"/> follows the same scope contract as the production
/// <c>CurrentTenant</c>: the previous state is restored when the returned
/// <see cref="IDisposable"/> is disposed.
/// </para>
/// </remarks>
public sealed class FakeCurrentTenant : ICurrentTenant
{
    private readonly AsyncLocal<TenantState?> _state = new();

    /// <inheritdoc/>
    public bool IsAvailable
    {
        get => _state.Value?.IsAvailable ?? false;
        set => EnsureState().IsAvailable = value;
    }

    /// <inheritdoc/>
    public Guid? Id
    {
        get => _state.Value?.Id;
        set
        {
            TenantState state = EnsureState();
            state.Id = value;
            state.IsAvailable = value.HasValue;
        }
    }

    /// <inheritdoc/>
    public string? Name
    {
        get => _state.Value?.Name;
        set => EnsureState().Name = value;
    }

    /// <inheritdoc/>
    public IDisposable Change(Guid? id, string? name = null)
    {
        TenantState? previous = _state.Value;
        _state.Value = new TenantState
        {
            Id = id,
            Name = name,
            IsAvailable = id.HasValue,
        };
        return new ChangeScope(this, previous);
    }

    private TenantState EnsureState() => _state.Value ??= new TenantState();

    private sealed class TenantState
    {
        public bool IsAvailable { get; set; }
        public Guid? Id { get; set; }
        public string? Name { get; set; }
    }

    private sealed class ChangeScope(FakeCurrentTenant owner, TenantState? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                owner._state.Value = previous;
            }
        }
    }
}
