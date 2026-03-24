namespace Granit.Domain;

/// <summary>
/// Interface for entities with a draft/published lifecycle.
/// When <see cref="IsPublished"/> is <c>false</c>, the entity is excluded from standard
/// queries by the EF Core global query filter registered by
/// <c>ModelBuilderExtensions.ApplyGranitConventions()</c> (WHERE IsPublished = true).
/// </summary>
/// <remarks>
/// Opt-in: only entities explicitly implementing this interface are filtered.
/// The filter can be bypassed at runtime via
/// <c>IDataFilter.Disable&lt;IPublishable&gt;()</c> for admin/editor views
/// that need to see drafts and archived versions.
/// </remarks>
public interface IPublishable
{
    /// <summary>Indicates whether the entity is the current published version.</summary>
    bool IsPublished { get; set; }
}
