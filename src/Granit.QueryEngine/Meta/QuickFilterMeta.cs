namespace Granit.QueryEngine.Meta;

/// <summary>
/// Quick filter metadata for frontend auto-configuration.
/// Quick filters are independent toggleable predicates (e.g. "My Appointments", "Unread").
/// </summary>
/// <param name="Name">Filter name (used in query string).</param>
/// <param name="Label">User-facing label.</param>
/// <param name="IsDefault">Whether this filter is active by default.</param>
public sealed record QuickFilterMeta(
    string Name,
    string Label,
    bool IsDefault);
