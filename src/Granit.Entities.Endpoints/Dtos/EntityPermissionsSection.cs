namespace Granit.Entities.Endpoints.Dtos;

/// <summary>
/// Permissions facet — boolean snapshot of which actions the requesting user is
/// allowed to perform on this entity. Computed server-side from the granted
/// permissions and the entity's <see cref="EntityIdentitySection.PermissionGroup"/>.
/// Inherits the standard `{Group}.{Resource}.{Action}` actions: <c>Read</c>,
/// <c>Create</c>, <c>Update</c>, <c>Delete</c>, <c>Manage</c>, <c>Execute</c>.
/// </summary>
/// <param name="CanRead">User has <c>{PermissionGroup}.Read</c>.</param>
/// <param name="CanCreate">User has <c>{PermissionGroup}.Create</c>.</param>
/// <param name="CanUpdate">User has <c>{PermissionGroup}.Update</c>.</param>
/// <param name="CanDelete">User has <c>{PermissionGroup}.Delete</c>.</param>
/// <param name="CanManage">User has <c>{PermissionGroup}.Manage</c>.</param>
/// <param name="CanExecute">User has <c>{PermissionGroup}.Execute</c>.</param>
public sealed record EntityPermissionsSection(
    bool CanRead,
    bool CanCreate,
    bool CanUpdate,
    bool CanDelete,
    bool CanManage,
    bool CanExecute);
