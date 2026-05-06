namespace Granit.Entities.Views;

/// <summary>
/// Closed set of permission constants used by the EntityView surface (ADR-047 §6).
/// All write operations are audited via <c>Granit.Auditing</c>.
/// </summary>
public static class EntityViewPermissions
{
    /// <summary>Permission group name (used as the i18n key prefix).</summary>
    public const string GroupName = "Entities.Views";

    /// <summary>Read views accessible to the current user (Personal owned + Shared targeted + Tenant).</summary>
    public const string Read = "Entities.Views.Read";

    /// <summary>Create a Personal view.</summary>
    public const string Create = "Entities.Views.Create";

    /// <summary>Promote a Personal view to Shared; modify own Shared views.</summary>
    public const string Share = "Entities.Views.Share";

    /// <summary>Promote a Shared view to Tenant; pin / unpin views; set tenant default.</summary>
    public const string Manage = "Entities.Views.Manage";

    /// <summary>Delete any view (moderation). Intended for tenant admins only.</summary>
    public const string DeleteAny = "Entities.Views.DeleteAny";
}
