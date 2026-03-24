namespace Granit.MultiTenancy;

/// <summary>
/// Null Object implementation of <see cref="ICurrentTenant"/>.
/// Registered by default via <c>TryAddSingleton</c> in <c>AddGranit&lt;T&gt;()</c>.
/// <c>Granit.MultiTenancy</c> replaces it with the real <c>AsyncLocal</c>-based implementation
/// when included in the module tree. <c>IsAvailable</c> is always false;
/// <c>Change()</c> is a no-op.
/// </summary>
internal sealed class NullTenantContext : ICurrentTenant
{
    internal static readonly NullTenantContext Instance = new();

    public bool IsAvailable => false;
    public Guid? Id => null;
    public string? Name => null;

    public IDisposable Change(Guid? id, string? name = null) => NullScope.Value;

    private sealed class NullScope : IDisposable
    {
        internal static readonly NullScope Value = new();
        public void Dispose() { }
    }
}
