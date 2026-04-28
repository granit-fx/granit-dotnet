namespace Granit.Parties.Domain;

/// <summary>
/// The functional purpose of a <see cref="PartyAddress"/> attached to a <see cref="Party"/>.
/// </summary>
/// <remarks>
/// A party may have any number of addresses with any combination of kinds. At most one
/// default address per <see cref="AddressKind"/> is enforced by the aggregate. The
/// <see cref="Other"/> kind covers everything that doesn't fit billing or shipping —
/// physical office, registered HQ when distinct from the billing address, returns
/// processing centre, etc.
/// </remarks>
public enum AddressKind
{
    /// <summary>The address used for invoices, statements, and tax / legal correspondence.</summary>
    Billing = 0,

    /// <summary>The address used to ship goods or deliver services.</summary>
    Shipping = 1,

    /// <summary>Any other address (HQ, returns centre, secondary office, …).</summary>
    Other = 2,
}
