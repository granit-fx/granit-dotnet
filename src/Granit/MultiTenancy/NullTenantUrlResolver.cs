namespace Granit.MultiTenancy;

/// <summary>
/// No-op <see cref="ITenantUrlResolver"/> used when <c>Granit.MultiTenancy</c> is not registered.
/// Always returns an empty string, signaling consumers to fall back to static configuration.
/// Replaced by <c>TenantUrlResolver</c> when the multi-tenancy module is loaded.
/// </summary>
internal sealed class NullTenantUrlResolver : ITenantUrlResolver
{
    public Task<string> ResolveBaseUrlAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(string.Empty);
}
