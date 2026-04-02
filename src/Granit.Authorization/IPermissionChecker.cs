namespace Granit.Authorization.Abstractions;

/// <summary>
/// Checks whether the current authenticated user has been granted the specified permission.
/// Implements the full RBAC check pipeline: AlwaysAllow → AdminRole bypass → cache → store.
/// </summary>
public interface IPermissionChecker
{
    /// <summary>
    /// Returns true if the current user is granted the specified permission through any of their roles.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if <paramref name="permissionName"/> has not been declared via <see cref="IPermissionDefinitionProvider"/>.
    /// </exception>
    Task<bool> IsGrantedAsync(string permissionName, CancellationToken cancellationToken = default);
}
