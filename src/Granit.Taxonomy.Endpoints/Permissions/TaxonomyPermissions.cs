namespace Granit.Taxonomy.Endpoints.Permissions;

/// <summary>
/// Permission constants exposed by <c>Granit.Taxonomy.Endpoints</c>.
/// </summary>
/// <remarks>
/// T2.1 ships only the Tag permissions. Category permissions
/// (<c>Taxonomy.Categories.Read</c> / <c>...Manage</c>) and the cross-entity
/// search permission (<c>Taxonomy.Search.Read</c>) arrive with their respective
/// stories (T4 and T3).
/// </remarks>
public static class TaxonomyPermissions
{
    /// <summary>Permission group name (used as the resource-key prefix for localisation).</summary>
    public const string GroupName = "Taxonomy";

    /// <summary>Permissions on tags.</summary>
    public static class Tags
    {
        /// <summary>Read tags (get, list, autocomplete).</summary>
        public const string Read = "Taxonomy.Tags.Read";

        /// <summary>Manage tags (create, rename, recolour, hide, delete).</summary>
        public const string Manage = "Taxonomy.Tags.Manage";
    }
}
