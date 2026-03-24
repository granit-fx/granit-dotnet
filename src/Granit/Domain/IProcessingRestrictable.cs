namespace Granit.Domain;

/// <summary>
/// Interface for RGPD processing restriction (Art. 18/21).
/// Marked entities are excluded from standard queries (analytics, marketing, etc.)
/// but remain in the database — no physical deletion.
/// </summary>
/// <remarks>
/// Same pattern as <see cref="ISoftDeletable"/>: a global query filter
/// <c>WHERE IsProcessingRestricted = false</c> is applied by
/// <c>ModelBuilderExtensions.ApplyGranitConventions()</c>.
/// The filter can be bypassed at runtime via
/// <c>IDataFilter.Disable&lt;IProcessingRestrictable&gt;()</c>
/// for admin/audit/legal access.
/// </remarks>
public interface IProcessingRestrictable
{
    /// <summary>Indicates whether processing has been restricted for this entity.</summary>
    bool IsProcessingRestricted { get; set; }

    /// <summary>Processing restriction timestamp (UTC).</summary>
    DateTimeOffset? ProcessingRestrictedAt { get; set; }

    /// <summary>Identifier of the user who requested the restriction.</summary>
    string? ProcessingRestrictedBy { get; set; }
}
