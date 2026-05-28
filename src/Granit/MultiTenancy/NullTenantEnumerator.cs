namespace Granit.MultiTenancy;

/// <summary>
/// Default <see cref="ITenantEnumerator"/> registered by base <c>Granit</c>. Returns an
/// empty list. Replaced by an <c>ITenantReader</c>-backed adapter when the application
/// loads the <c>Granit.MultiTenancy</c> package.
/// </summary>
internal sealed class NullTenantEnumerator : ITenantEnumerator
{
    /// <summary>Shared singleton — the type holds no state.</summary>
    public static readonly NullTenantEnumerator Instance = new();

    private NullTenantEnumerator() { }

    public Task<IReadOnlyList<(Guid Id, string Name)>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<(Guid, string)>>([]);
}
