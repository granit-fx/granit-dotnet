using Granit.MultiTenancy;

namespace Granit.Testing.Fakes;

/// <summary>
/// Shared mutable implementation of <see cref="ICurrentTenant"/> for web integration tests.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="FakeCurrentTenant"/>, state is stored as plain fields — no
/// <see cref="AsyncLocal{T}"/>. This makes it suitable for <c>WebApplicationFactory</c>
/// test hosts where the test code and the ASP.NET Core request pipeline run in separate
/// async contexts: a mutation in the test context must be visible to the server context,
/// which is a sibling (not a descendant) in the execution-context tree.
/// </para>
/// <para>
/// <b>Do not use for parallel unit tests</b> — the shared mutable state is not isolated
/// between concurrent tests. Use <see cref="FakeCurrentTenant"/> instead.
/// </para>
/// <para>
/// <see cref="Change"/> follows the same scope contract as the production
/// <c>CurrentTenant</c>: the previous state is restored when the returned
/// <see cref="IDisposable"/> is disposed.
/// </para>
/// </remarks>
public sealed class SharedCurrentTenant : ICurrentTenant
{
    /// <inheritdoc/>
    public bool IsAvailable { get; set; }

    /// <inheritdoc/>
    public Guid? Id { get; set; }

    /// <inheritdoc/>
    public string? Name { get; set; }

    /// <inheritdoc/>
    public string? Jurisdiction { get; set; }

    /// <inheritdoc/>
    public IDisposable Change(Guid? id, string? name = null, string? jurisdiction = null)
    {
        Guid? prevId = Id;
        string? prevName = Name;
        bool prevAvailable = IsAvailable;
        string? prevJurisdiction = Jurisdiction;

        Id = id;
        Name = name;
        IsAvailable = id.HasValue;
        Jurisdiction = jurisdiction;

        return new ChangeScope(this, prevId, prevName, prevAvailable, prevJurisdiction);
    }

    private sealed class ChangeScope(
        SharedCurrentTenant owner,
        Guid? prevId,
        string? prevName,
        bool prevAvailable,
        string? prevJurisdiction) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                owner.Id = prevId;
                owner.Name = prevName;
                owner.IsAvailable = prevAvailable;
                owner.Jurisdiction = prevJurisdiction;
            }
        }
    }
}
