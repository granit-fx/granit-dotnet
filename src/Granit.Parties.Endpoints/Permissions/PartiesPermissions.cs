namespace Granit.Parties.Endpoints.Permissions;

/// <summary>Permission constants for the contacts administration endpoints.</summary>
public static class PartiesPermissions
{
    /// <summary>Permission group name.</summary>
    public const string GroupName = "Parties";

    /// <summary>Permissions for the contact resource.</summary>
    public static class Parties
    {
        /// <summary>Grants read access to contacts (list + by-id + by-external-id).</summary>
        public const string Read = "Parties.Parties.Read";

        /// <summary>
        /// Grants identity write access (create / update display fields, addresses, emails,
        /// phones, roles). Operational maintenance permission. Does NOT grant lifecycle
        /// transitions, tax-status changes, or external-mapping registration — those are
        /// scoped under their own permissions for least-privilege (ISO 27001 A.5.15).
        /// </summary>
        public const string Manage = "Parties.Parties.Manage";

        /// <summary>
        /// Grants lifecycle transitions (suspend / activate / archive). Separated from
        /// <see cref="Manage"/> because archival is terminal and suspension may carry
        /// commercial / fraud-investigation implications.
        /// </summary>
        public const string Lifecycle = "Parties.Parties.Lifecycle";

        /// <summary>
        /// Grants the right to set or clear the customer-specific <c>TaxStatus</c>
        /// (exempt / reverse-charge). Separated from <see cref="Manage"/> because
        /// <c>Granit.Tax</c> consumes this status to compute 0% rates on every invoice
        /// line — a financial-impact operation that warrants its own audit and approval
        /// path (ISO 27001 A.5.15, GDPR Art. 25).
        /// </summary>
        public const string SetTaxStatus = "Parties.Parties.SetTaxStatus";

        /// <summary>
        /// Grants the right to register / remove external provider identifiers (Stripe,
        /// Mollie, Odoo, Sage, NetSuite, …). Separated from <see cref="Manage"/> because
        /// the mapping is the trust anchor between Granit and a third-party billing
        /// system — a misregistered identifier can route customer money to the wrong
        /// account.
        /// </summary>
        public const string ExternalMappings = "Parties.Parties.ExternalMappings";
    }
}
