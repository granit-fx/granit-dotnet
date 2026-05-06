namespace Granit.Taxonomy.Endpoints.Permissions;

/// <summary>
/// Permission constants exposed by <c>Granit.Taxonomy.Endpoints</c>.
/// </summary>
/// <remarks>
/// Tag permissions ship in T2.1, the cross-entity search permission lands in T3.1.
/// Category permissions (<c>Taxonomy.Categories.Read</c> / <c>...Manage</c>) arrive
/// with story T4.
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

    /// <summary>Permissions on hierarchical categories.</summary>
    public static class Categories
    {
        /// <summary>Read categories (get, list, breadcrumb).</summary>
        public const string Read = "Taxonomy.Categories.Read";

        /// <summary>Manage categories (create, rename, move, hide, delete, assign / unassign on targets).</summary>
        public const string Manage = "Taxonomy.Categories.Manage";
    }

    /// <summary>Permissions on the cross-entity search endpoint.</summary>
    /// <remarks>
    /// Separate from <see cref="Tags.Read"/> because the search results expose
    /// <c>TargetId</c>s the caller may not have per-entity read on. Hosts that
    /// grant <c>Taxonomy.Search.Read</c> MUST combine the result with their
    /// own per-entity ACL when rendering.
    /// </remarks>
    public static class Search
    {
        /// <summary>Run cross-entity tag searches that expose target ids across modules.</summary>
        public const string Read = "Taxonomy.Search.Read";
    }
}
