namespace Granit.Parties.Domain;

/// <summary>
/// Reserved provider names for <see cref="PartyExternalMapping.ProviderName"/>.
/// </summary>
/// <remarks>
/// External providers should consult this registry before inventing a name to avoid
/// collisions. The list is non-exhaustive — third-party providers register their own
/// names freely. The <see cref="Tenant"/> value is reserved for the reverse-link
/// between a host-scoped contact and the tenant entity it represents.
/// </remarks>
public static class PartyExternalProviderNames
{
    /// <summary>
    /// Reverse-link from a host-scoped <see cref="Party"/> to the tenant entity it
    /// represents in the SaaS host's billing relationship.
    /// </summary>
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

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        Tenant, Stripe, Mollie, Odoo, Sage, NetSuite,
    };

    /// <summary>
    /// Returns <c>true</c> when <paramref name="providerName"/> is one of the reserved
    /// provider names declared on this class. Used by the metrics layer to bound the
    /// cardinality of the <c>provider_name</c> tag — unrecognised values are folded to
    /// a single <c>"other"</c> bucket to prevent a metric-cardinality DoS via a stream
    /// of distinct random provider names (CWE-770).
    /// </summary>
    public static bool IsReserved(string? providerName) =>
        providerName is not null && Reserved.Contains(providerName);
}
