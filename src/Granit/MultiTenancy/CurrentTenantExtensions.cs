namespace Granit.MultiTenancy;

/// <summary>
/// Extension methods for <see cref="ICurrentTenant"/>.
/// </summary>
public static class CurrentTenantExtensions
{
    /// <summary>
    /// Temporarily switches to the host context (no active tenant).
    /// Host data consists of entities with <c>TenantId = null</c>.
    /// The previous tenant is restored when the returned scope is disposed.
    /// </summary>
    /// <param name="currentTenant">The current tenant service.</param>
    /// <returns>A disposable scope that restores the previous tenant on disposal.</returns>
    public static IDisposable ChangeToHost(this ICurrentTenant currentTenant)
    {
        ArgumentNullException.ThrowIfNull(currentTenant);
        return currentTenant.Change(null);
    }
}
