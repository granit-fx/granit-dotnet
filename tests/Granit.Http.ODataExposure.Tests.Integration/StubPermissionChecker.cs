using Granit.Authorization;

namespace Granit.Http.ODataExposure.Tests.Integration;

/// <summary>
/// Permission checker with a configurable verdict — grants everything by
/// default (the tenant-isolation / hardening suites don't depend on
/// permissions), while the #3005 $metadata authorization matrix injects a
/// per-permission predicate to drive the 403 leg without a real
/// authorization stack.
/// </summary>
internal sealed class StubPermissionChecker(Func<string, bool>? isGranted = null) : IPermissionChecker
{
    private readonly Func<string, bool> _isGranted = isGranted ?? (_ => true);

    public Task<bool> IsGrantedAsync(string permissionName, CancellationToken cancellationToken = default) =>
        Task.FromResult(_isGranted(permissionName));

    public Task<IReadOnlyList<string>> GetGrantedAsync(
        IReadOnlyList<string> permissionNames,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<string>>([.. permissionNames.Where(_isGranted)]);
}
