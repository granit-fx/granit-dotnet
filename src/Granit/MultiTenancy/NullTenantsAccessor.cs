namespace Granit.MultiTenancy;

/// <summary>
/// Default <see cref="ITenantsAccessor"/> registered by base <c>Granit</c>. Returns an
/// empty list. Replaced by an <c>ITenantReader</c>-backed adapter when the application
/// loads the <c>Granit.MultiTenancy</c> package.
/// </summary>
internal sealed class NullTenantsAccessor : ITenantsAccessor
{
    /// <summary>Shared singleton — the type holds no state.</summary>
    public static readonly NullTenantsAccessor Instance = new();

    private NullTenantsAccessor() { }

    public Task<IReadOnlyList<(Guid Id, string Name)>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<(Guid, string)>>([]);
}
