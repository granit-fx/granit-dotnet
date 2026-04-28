namespace Granit.Dashboards.Domain;

/// <summary>
/// Lifecycle state of a persisted <see cref="Dashboard"/> aggregate. Transitions are
/// strict and one-way except <c>Archived → Draft</c> (admin restore action).
/// </summary>
public enum DashboardStatus
{
    /// <summary>Default state on creation. Visible only to its <c>CreatedBy</c> editor — not surfaced to other admins.</summary>
    Draft = 0,

    /// <summary>Surfaced in the catalogue, served to all users with the relevant permissions.</summary>
    Published = 1,

    /// <summary>Hidden from the catalogue but kept for audit / restore. Restorable to <see cref="Draft"/>.</summary>
    Archived = 2,
}
