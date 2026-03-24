namespace Granit.Domain;

/// <summary>
/// Interface for entities with an active/inactive status.
/// When <see cref="IsActive"/> is <c>false</c>, the entity is excluded from standard
/// queries by the EF Core global query filter registered by
/// <c>ModelBuilderExtensions.ApplyGranitConventions()</c> (WHERE IsActive = true).
/// </summary>
/// <remarks>
/// Opt-in: only entities explicitly implementing this interface are filtered.
/// The filter can be bypassed at runtime via
/// <c>IDataFilter.Disable&lt;IActive&gt;()</c>.
/// </remarks>
public interface IActive
{
    /// <summary>Indicates whether the entity is active and visible in standard queries.</summary>
    bool IsActive { get; set; }
}
