namespace Granit.Customers.Domain;

/// <summary>
/// Reserved provider names for <see cref="CustomerExternalMapping.ProviderName"/>.
/// </summary>
/// <remarks>
/// External providers should consult this registry before inventing a name to avoid
/// collisions. The list is non-exhaustive — third-party providers register their own
/// names freely. The <see cref="Tenant"/> value is reserved for the reverse-link
/// between a host-scoped customer and the tenant entity it represents.
/// </remarks>
public static class CustomerExternalProviderNames
{
    /// <summary>
    /// Reverse-link from a host-scoped <see cref="Customer"/> to the tenant entity it
    /// represents in the SaaS host's billing relationship.
    /// </summary>
    /// <remarks>
    /// Used by <c>IDefaultCustomerResolver</c> to find the host-scoped customer for a
    /// given tenant. The <see cref="CustomerExternalMapping.ExternalId"/> stores the
    /// tenant <see cref="System.Guid"/> as a string.
    /// </remarks>
    public const string Tenant = "tenant";

    /// <summary>Stripe customer identifier (e.g., <c>cus_1234</c>).</summary>
    public const string Stripe = "stripe";

    /// <summary>Mollie customer identifier (e.g., <c>cst_1234</c>).</summary>
    public const string Mollie = "mollie";

    /// <summary>Odoo <c>res.partner</c> integer identifier (stored as string).</summary>
    public const string Odoo = "odoo";

    /// <summary>Sage customer identifier.</summary>
    public const string Sage = "sage";

    /// <summary>NetSuite customer identifier.</summary>
    public const string NetSuite = "netsuite";
}
