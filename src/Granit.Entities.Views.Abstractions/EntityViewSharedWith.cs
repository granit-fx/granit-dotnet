namespace Granit.Entities.Views;

/// <summary>
/// Audience descriptor for a <see cref="EntityViewVisibility.Shared"/> view: the
/// roles + users that may load the view. Both lists may be empty, but the resulting
/// view is then unreachable; the framework rejects empty Shared at create / update
/// time.
/// </summary>
/// <param name="Roles">Role names (case-sensitive) granted access to the view.</param>
/// <param name="Users">User identifiers granted access to the view.</param>
public sealed record EntityViewSharedWith(IReadOnlyList<string> Roles, IReadOnlyList<Guid> Users)
{
    /// <summary>Empty audience — used as a placeholder for Personal / Tenant views.</summary>
    public static EntityViewSharedWith Empty { get; } = new([], []);
}
