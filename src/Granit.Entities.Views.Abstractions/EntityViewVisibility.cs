namespace Granit.Entities.Views;

/// <summary>
/// Closed enum of visibility levels an <c>EntityView</c> may carry — see ADR-047 §4.
/// </summary>
public enum EntityViewVisibility
{
    /// <summary>Owner-only. Created by any user with <c>Entities.Views.Create</c>.</summary>
    Personal,

    /// <summary>Visible to specific roles + users. Promoted from Personal by a user with <c>Entities.Views.Share</c>.</summary>
    Shared,

    /// <summary>Visible to the entire tenant. Promoted by an admin with <c>Entities.Views.Manage</c>.</summary>
    Tenant,
}
