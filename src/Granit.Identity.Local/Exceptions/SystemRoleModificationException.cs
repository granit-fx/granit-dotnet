namespace Granit.Identity.Local.Exceptions;

/// <summary>The mutation a caller attempted on a system role.</summary>
public enum SystemRoleOperation
{
    /// <summary>Rename / update of the role.</summary>
    Rename,

    /// <summary>Hard-delete of the role.</summary>
    Delete,
}

/// <summary>
/// Thrown by the role orchestrator when a caller attempts to rename or delete a
/// platform-provisioned system role (<c>RoleMetadata.IsSystem</c>).
/// </summary>
/// <remarks>
/// Lets the endpoint return a localized 403 keyed on <see cref="Operation"/> instead of
/// string-matching <c>"system role"</c> in the message. Derives from
/// <see cref="InvalidOperationException"/> for back-compat with existing generic catch blocks.
/// </remarks>
public sealed class SystemRoleModificationException(string roleName, SystemRoleOperation operation)
    : InvalidOperationException($"Role '{roleName}' is a system role and cannot be {(operation == SystemRoleOperation.Rename ? "renamed" : "deleted")}.")
{
    /// <summary>The name of the system role that was targeted.</summary>
    public string RoleName { get; } = roleName;

    /// <summary>The attempted mutation.</summary>
    public SystemRoleOperation Operation { get; } = operation;
}
