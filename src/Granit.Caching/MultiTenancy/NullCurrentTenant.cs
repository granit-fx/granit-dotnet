using Granit.MultiTenancy;

namespace Granit.Caching.MultiTenancy;

/// <summary>
/// No-op <see cref="ICurrentTenant"/> fallback for standalone
/// <c>AddGranitCaching()</c> usage without the full Granit module tree.
/// All cache keys are prefixed with <c>t:host:</c>.
/// </summary>
internal sealed class NullCurrentTenant : ICurrentTenant
{
    public bool IsAvailable => false;
    public Guid? Id => null;
    public string? Name => null;
    public IDisposable Change(Guid? id, string? name = null) => NullDisposable.Instance;

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose() { }
    }
}
