namespace Granit.Contacts.Domain;

/// <summary>
/// Set of roles a <see cref="Contact"/> simultaneously plays. A single contact can be a
/// customer (we sell to it), a supplier (we buy from it), an employee, and a lead — all
/// at the same time. Each downstream module filters by its relevant role flag.
/// </summary>
[Flags]
public enum ContactRoles
{
    /// <summary>No role set. A contact in this state is technically valid (e.g. fresh import) but won't be picked up by any role-filtered query.</summary>
    None = 0,

    /// <summary>The contact is a customer of the platform / tenant. Consumed by Invoicing, Subscriptions, Payments, Tax, CustomerBalance.</summary>
    Customer = 1 << 0,

    /// <summary>The contact is a supplier / vendor. Reserved for the future <c>Granit.Procurement</c> module.</summary>
    Supplier = 1 << 1,

    /// <summary>The contact is an internal employee. Reserved for the future <c>Granit.Hr</c> module.</summary>
    Employee = 1 << 2,

    /// <summary>The contact is a sales lead / prospect. Reserved for the future <c>Granit.Crm</c> module.</summary>
    Lead = 1 << 3,
}
