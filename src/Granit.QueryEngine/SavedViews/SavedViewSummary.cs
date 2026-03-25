namespace Granit.QueryEngine.SavedViews;

/// <summary>
/// Lightweight summary of a saved view for inclusion in <see cref="Meta.QueryMetadata"/>.
/// </summary>
/// <param name="Id">The saved view identifier.</param>
/// <param name="Name">User-facing name.</param>
/// <param name="IsShared">Whether this view is shared.</param>
/// <param name="IsDefault">Whether this is the user's default view.</param>
public sealed record SavedViewSummary(
    Guid Id,
    string Name,
    bool IsShared,
    bool IsDefault);
