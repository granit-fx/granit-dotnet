using Granit.Authorization;

namespace Granit.Http.ODataExposure.Tests.Integration;

/// <summary>
/// Permission checker that grants every check — the OData layer's tenant
/// isolation tests don't depend on permissions and we need a single
/// non-null implementation registered to satisfy the route handler's DI.
/// Permission gating itself is exercised in the unit-test project.
/// </summary>
internal sealed class StubPermissionChecker : IPermissionChecker
{
    public Task<bool> IsGrantedAsync(string permissionName, CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    public Task<IReadOnlyList<string>> GetGrantedAsync(
        IReadOnlyList<string> permissionNames,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>(permissionNames);
}
