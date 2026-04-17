namespace Granit.Domain;

/// <summary>
/// Marker interface for entities that expose an on/off activation flag and should be
/// hidden from standard queries when deactivated.
/// </summary>
/// <remarks>
/// <para>
/// When <see cref="Activated"/> is <c>false</c>, the entity is excluded from standard
/// queries by the EF Core global query filter registered by
/// <c>ModelBuilderExtensions.ApplyGranitConventions()</c> (<c>WHERE Activated = true</c>).
/// Admin code paths that need to see deactivated rows disable the filter via
/// <c>IDataFilter.Disable&lt;IActive&gt;()</c>.
/// </para>
/// <para>
/// The interface requires only a getter — implementers may expose a public setter
/// (CRUD / reference-data style) or encapsulate mutation via behavior methods with a
/// private setter (DDD aggregate style). The query filter only reads the value; no
/// framework code mutates <see cref="Activated"/> through the interface.
/// </para>
/// <para>
/// The past-participle naming (<em>Activated</em>) pairs with the conventional
/// <c>Activate()</c> / <c>Deactivate()</c> behavior methods found on DDD aggregates and
/// mirrors the bare-participle convention used in the Identity subsystem
/// (<c>IIdentityUser.Enabled</c>).
/// </para>
/// </remarks>
public interface IActive
{
    /// <summary>Indicates whether the entity is currently active and visible in standard queries.</summary>
    bool Activated { get; }
}
