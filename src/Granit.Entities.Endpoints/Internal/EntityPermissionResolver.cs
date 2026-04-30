using Granit.Authorization;

namespace Granit.Entities.Endpoints.Internal;

/// <summary>
/// Resolves the standard <c>{Group}.{Resource}.{Action}</c> permission strings for
/// an entity definition (per CLAUDE.md naming convention) and asks
/// <see cref="IPermissionChecker"/> which ones are granted to the current caller.
/// </summary>
/// <remarks>
/// The convention exposes <c>Read</c>, <c>Create</c>, <c>Update</c>, <c>Delete</c>,
/// <c>Manage</c>, <c>Execute</c>. Modules that don't declare a particular action
/// surface <see langword="false"/> from <see cref="IPermissionChecker.GetGrantedAsync"/>;
/// that is — by design — indistinguishable from "the user does not have it" on the
/// client. The point is the user can't act on a permission that doesn't exist.
/// </remarks>
internal sealed class EntityPermissionResolver(IPermissionChecker checker)
{
    private static readonly string[] StandardActions =
        ["Read", "Create", "Update", "Delete", "Manage", "Execute"];

    public async Task<EntityPermissionSnapshot> ResolveAsync(
        string? permissionGroup, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(permissionGroup))
        {
            // No permission group declared — the entity is implicitly public to
            // any authenticated user. The discovery filter falls back to "show
            // every public entity"; the manifest exposes false on every flag.
            return EntityPermissionSnapshot.AllPublic;
        }

        string[] candidates = new string[StandardActions.Length];
        for (int i = 0; i < StandardActions.Length; i++)
        {
            candidates[i] = $"{permissionGroup}.{StandardActions[i]}";
        }

        IReadOnlyList<string> granted = await checker
            .GetGrantedAsync(candidates, cancellationToken)
            .ConfigureAwait(false);

        HashSet<string> grantedSet = new(granted, StringComparer.Ordinal);

        return new EntityPermissionSnapshot(
            CanRead: grantedSet.Contains(candidates[0]),
            CanCreate: grantedSet.Contains(candidates[1]),
            CanUpdate: grantedSet.Contains(candidates[2]),
            CanDelete: grantedSet.Contains(candidates[3]),
            CanManage: grantedSet.Contains(candidates[4]),
            CanExecute: grantedSet.Contains(candidates[5]),
            IsPublic: false);
    }

    /// <summary>
    /// Returns the subset of <paramref name="permissionNames"/> that the
    /// current caller has been granted. Wraps <see cref="IPermissionChecker.GetGrantedAsync"/>
    /// so manifest filtering goes through one DI seam.
    /// </summary>
    public Task<IReadOnlyList<string>> CheckBatchAsync(
        IReadOnlyCollection<string> permissionNames,
        CancellationToken cancellationToken) =>
        checker.GetGrantedAsync([.. permissionNames], cancellationToken);
}

/// <summary>Result of <see cref="EntityPermissionResolver.ResolveAsync"/>.</summary>
/// <param name="CanRead">User has the entity's <c>Read</c> permission, or the entity has no permission group declared.</param>
/// <param name="CanCreate">User has <c>Create</c>.</param>
/// <param name="CanUpdate">User has <c>Update</c>.</param>
/// <param name="CanDelete">User has <c>Delete</c>.</param>
/// <param name="CanManage">User has <c>Manage</c>.</param>
/// <param name="CanExecute">User has <c>Execute</c>.</param>
/// <param name="IsPublic">When <see langword="true"/>, the entity has no permission group — discovery treats it as visible to every authenticated user.</param>
internal sealed record EntityPermissionSnapshot(
    bool CanRead,
    bool CanCreate,
    bool CanUpdate,
    bool CanDelete,
    bool CanManage,
    bool CanExecute,
    bool IsPublic)
{
    public static EntityPermissionSnapshot AllPublic { get; } =
        new(false, false, false, false, false, false, IsPublic: true);

    /// <summary>Returns <see langword="true"/> when discovery should surface the entity.</summary>
    public bool IsVisible => IsPublic || CanRead;
}
