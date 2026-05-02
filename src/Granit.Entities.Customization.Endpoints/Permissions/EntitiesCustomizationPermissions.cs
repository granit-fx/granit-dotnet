namespace Granit.Entities.Customization.Endpoints.Permissions;

/// <summary>
/// Permission constants for the <c>Granit.Entities.Customization.Endpoints</c> module.
/// </summary>
public static class EntitiesCustomizationPermissions
{
    /// <summary>Permission group name (CLAUDE.md <c>[Group].[Resource].[Action]</c>).</summary>
    public const string GroupName = "EntitiesCustomization";

    /// <summary>Permissions for the customization resource.</summary>
    public static class Customizations
    {
        /// <summary>Read the tenant's customizations (GET).</summary>
        public const string Read = "EntitiesCustomization.Customizations.Read";

        /// <summary>Write or delete the tenant's customizations (PUT / DELETE).</summary>
        public const string Manage = "EntitiesCustomization.Customizations.Manage";
    }
}
