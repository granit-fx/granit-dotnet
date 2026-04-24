namespace Granit.Catalog.Endpoints.Permissions;

/// <summary>Permission constants for Granit.Catalog.Endpoints.</summary>
public static class CatalogPermissions
{
    /// <summary>Permission group name.</summary>
    public const string GroupName = "Catalog";

    /// <summary>Product resource permissions.</summary>
    public static class Products
    {
        /// <summary>View products (any lifecycle status).</summary>
        public const string Read = "Catalog.Products.Read";

        /// <summary>Manage products (create, update, publish, archive, external mappings).</summary>
        public const string Manage = "Catalog.Products.Manage";
    }
}
