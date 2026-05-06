namespace Granit.MultiTenancy.Stores;

/// <summary>
/// No-op <see cref="IUserTenantMembershipReader"/> used when no concrete membership
/// store is registered. Returns <see langword="true"/> for every request.
/// </summary>
/// <remarks>
/// SECURITY: this fallback intentionally does not enforce membership — it preserves
/// existing behavior for deployments that have not yet wired a real reader. To
/// activate enforcement, the host must (a) register a concrete
/// <see cref="IUserTenantMembershipReader"/> AND (b) set
/// <c>MultiTenancyOptions.RequireMembershipCheck = true</c>. The latter is
/// validated at startup against the registered reader to fail-fast on misconfiguration.
/// </remarks>
internal sealed class NullUserTenantMembershipReader : IUserTenantMembershipReader
{
    public Task<bool> IsMemberAsync(string userId, Guid tenantId, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);
}
