using System.Collections.Frozen;

namespace Granit.Http.Cookies;

/// <summary>
/// Canonical snake_case wire names of the <see cref="CookieCategory"/> values —
/// the vocabulary shared by the CMP config endpoint, the consent capture endpoint,
/// and the consent ledger.
/// </summary>
public static class CookieCategoryNames
{
    /// <summary>All known snake_case category names (ordinal comparison).</summary>
    public static readonly FrozenSet<string> All =
        Enum.GetValues<CookieCategory>().Select(ToSnakeCase).ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Maps a <see cref="CookieCategory"/> to its snake_case wire name.</summary>
    public static string ToSnakeCase(CookieCategory category) => category switch
    {
        CookieCategory.StrictlyNecessary => "strictly_necessary",
        CookieCategory.Preferences => "preferences",
        CookieCategory.Analytics => "analytics",
        CookieCategory.Marketing => "marketing",
        CookieCategory.SaleOrSharing => "sale_or_sharing",
        // Lower-casing a multi-word enum name glues its words together — every
        // category must have an explicit snake_case mapping above.
        _ => category.ToString().ToLowerInvariant(),
    };

    /// <summary>Returns <c>true</c> when <paramref name="name"/> is a known snake_case category name.</summary>
    public static bool IsKnown(string? name) => name is not null && All.Contains(name);
}
