using Microsoft.Extensions.DependencyInjection;

namespace Granit.Authorization;

/// <summary>
/// Singleton-safe <see cref="IPermissionChecker"/> wrapper for callers that must enforce
/// <see cref="IPermissionChecker"/> from a singleton context (hosted services, pool
/// owners, registration-time guards, …).
/// </summary>
/// <remarks>
/// <para>
/// The default <see cref="IPermissionChecker"/> registration is <c>Scoped</c> because it
/// depends on per-request state (current user, current tenant). Capturing it directly in
/// a singleton's constructor is a captive dependency: it fails <c>ValidateScopes</c>
/// (default in Development) and, when validation is off, leaks the first request's scope
/// across every subsequent request — silently bypassing per-user/per-tenant isolation.
/// </para>
/// <para>
/// This wrapper takes an <see cref="IServiceScopeFactory"/> (singleton) and creates a
/// fresh DI scope per <see cref="IsGrantedAsync"/> / <see cref="GetGrantedAsync"/> call,
/// resolving the real checker from that scope. The underlying request-context resolution
/// (<c>IHttpContextAccessor</c>, <c>ICurrentUserService</c>, AsyncLocal tenant…)
/// continues to see the right values because those primitives are not scope-bound.
/// </para>
/// <para>
/// Construction is gated by <see cref="TryCreate(IServiceScopeFactory?)"/>: when no
/// <see cref="IPermissionChecker"/> is registered the factory returns <c>null</c>, so
/// callers can preserve the conventional "no checker registered → no enforcement"
/// semantic via a simple null-check (instead of this wrapper having to choose between
/// fail-open and fail-closed when the host hasn't installed authorization at all).
/// </para>
/// </remarks>
public sealed class ScopedPermissionChecker(IServiceScopeFactory scopeFactory) : IPermissionChecker
{
    /// <summary>
    /// Returns a wrapper bound to <paramref name="scopeFactory"/>, or <c>null</c> when
    /// <see cref="IPermissionChecker"/> is not registered in the host.
    /// </summary>
    public static ScopedPermissionChecker? TryCreate(IServiceScopeFactory? scopeFactory)
    {
        if (scopeFactory is null)
        {
            return null;
        }
        using IServiceScope probe = scopeFactory.CreateScope();
        if (probe.ServiceProvider.GetService<IPermissionChecker>() is null)
        {
            return null;
        }
        return new ScopedPermissionChecker(scopeFactory);
    }

    /// <inheritdoc/>
    public async Task<bool> IsGrantedAsync(string permissionName, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IPermissionChecker? inner = scope.ServiceProvider.GetService<IPermissionChecker>();
        if (inner is null)
        {
            // Defensive: the probe at TryCreate said inner was registered, but DI state
            // could in theory have changed. Deny when we can't verify — least-privilege.
            return false;
        }
        return await inner.IsGrantedAsync(permissionName, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetGrantedAsync(
        IReadOnlyList<string> permissionNames,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IPermissionChecker? inner = scope.ServiceProvider.GetService<IPermissionChecker>();
        if (inner is null)
        {
            return [];
        }
        return await inner.GetGrantedAsync(permissionNames, cancellationToken).ConfigureAwait(false);
    }
}
