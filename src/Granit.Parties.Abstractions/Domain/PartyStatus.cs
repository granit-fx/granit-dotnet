namespace Granit.Parties.Domain;

/// <summary>Lifecycle status of a <see cref="Party"/>.</summary>
/// <remarks>
/// Allowed transitions:
/// <list type="bullet">
/// <item><see cref="Active"/> ↔ <see cref="Suspended"/></item>
/// <item><see cref="Active"/> → <see cref="Archived"/></item>
/// <item><see cref="Suspended"/> → <see cref="Archived"/></item>
/// </list>
/// <para>
/// <see cref="Archived"/> is terminal: archived parties cannot be reactivated and
/// can no longer be edited. The row is preserved (legal retention).
/// </para>
/// </remarks>
public enum PartyStatus
{
    /// <summary>Default state on creation.</summary>
    Active = 0,

    /// <summary>Temporarily blocked. Restore via <c>Activate()</c>.</summary>
    Suspended = 1,

    /// <summary>Terminal state.</summary>
    Archived = 2,
}
