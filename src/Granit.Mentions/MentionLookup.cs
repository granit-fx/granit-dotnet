namespace Granit.Mentions;

/// <summary>
/// Well-known identifiers for the <c>@</c> mention picker, which is served by
/// <c>Granit.DataLookup</c> through a single facade source.
/// </summary>
public static class MentionLookup
{
    /// <summary>
    /// The <c>ILookupSource</c> name under which the mention facade is exposed
    /// (<c>GET /lookups/mentions</c>).
    /// </summary>
    public const string SourceName = "mentions";

    /// <summary>
    /// The optional <c>scope</c> key that narrows a mention search to one type
    /// (<c>GET /lookups/mentions?scope.type=user</c>).
    /// </summary>
    public const string TypeScopeKey = "type";
}
