using Granit.Customers.Domain.ValueObjects;
using Granit.Customers.Events;
using Granit.DataProtection;
using Granit.Domain;

namespace Granit.Customers.Domain;

/// <summary>
/// Central billing identity in Granit. A customer holds the legal name, primary contact,
/// billing address, default currency, lifecycle status, and a polyglot collection of
/// external provider identifiers (Stripe customer ID, Mollie customer ID, Odoo
/// <c>res.partner</c> ID, …). Replaces today's per-provider mapping aggregates and the
/// per-invoice billing-address duplication.
/// </summary>
/// <remarks>
/// <para><b>Dual-use design.</b> Customer is <see cref="IMultiTenant"/> by design:</para>
/// <list type="bullet">
/// <item><c>TenantId == null</c> ⇒ <i>host-scoped</i>: the SaaS host's B2B customers (its tenants viewed as billable entities).</item>
/// <item><c>TenantId == &lt;tenant&gt;</c> ⇒ <i>tenant-scoped</i>: an e-commerce app on a tenant tracking its own end-customers.</item>
/// </list>
/// <para>Both scopes share the same table and behavior; the multi-tenant query filter
/// (<see cref="IMultiTenant"/>) enforces isolation transparently. A tenant admin sees only
/// their own customers; a host admin (no tenant context) sees host-scoped customers.</para>
/// <para><b>Lifecycle.</b> <see cref="CustomerStatus.Active"/> ↔ <see cref="CustomerStatus.Suspended"/>.
/// Both states can transition to terminal <see cref="CustomerStatus.Archived"/>. Archived
/// customers are not reactivatable; the row is retained for accounting integrity.</para>
/// <para><b>External mappings.</b> A customer can carry at most one mapping per provider
/// (uniqueness enforced by the EF configuration in <c>Granit.Customers.EntityFrameworkCore</c>).
/// The aggregate enforces uniqueness defensively at <see cref="AddExternalMapping"/> too.</para>
/// </remarks>
public sealed class Customer : AuditedAggregateRoot, IMultiTenant
{
    private readonly List<CustomerExternalMapping> _externalMappings = [];

    private Customer() { }

    /// <summary>Creates a new customer in <see cref="CustomerStatus.Active"/> status.</summary>
    /// <param name="id">Unique identifier.</param>
    /// <param name="tenantId">Owning tenant identifier, or <c>null</c> for host-scoped customers.</param>
    /// <param name="legalName">Legal display name (required, max 256 chars).</param>
    /// <param name="defaultCurrency">ISO 4217 alpha-3 currency code (required, exactly 3 chars).</param>
    /// <param name="email">Primary contact email (optional, max 320 chars).</param>
    /// <param name="billingAddress">Optional billing address.</param>
    /// <param name="timezone">IANA timezone (optional; defaults to <c>"UTC"</c>).</param>
    public static Customer Create(
        Guid id,
        Guid? tenantId,
        string legalName,
        string defaultCurrency,
        string? email = null,
        BillingAddress? billingAddress = null,
        string? timezone = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legalName);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCurrency);
        if (defaultCurrency.Length != 3)
        {
            throw new ArgumentException(
                "DefaultCurrency must be a 3-letter ISO 4217 code.", nameof(defaultCurrency));
        }

        var customer = new Customer
        {
            Id = id,
            TenantId = tenantId,
            LegalName = legalName,
            Email = email,
            BillingAddress = billingAddress,
            DefaultCurrency = defaultCurrency.ToUpperInvariant(),
            Timezone = string.IsNullOrWhiteSpace(timezone) ? "UTC" : timezone,
            Status = CustomerStatus.Active,
        };

        customer.AddDomainEvent(new CustomerCreatedEvent(
            CustomerId.Create(id), tenantId, legalName));
        customer.AddDistributedEvent(new CustomerCreatedEto(
            CustomerId.Create(id), tenantId, legalName, customer.DefaultCurrency));

        return customer;
    }

    /// <summary>Legal display name.</summary>
    [SensitiveData(Level = Sensitivity.Internal)]
    public string LegalName { get; private set; } = string.Empty;

    /// <summary>Primary contact email (optional).</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    public string? Email { get; private set; }

    /// <summary>Optional postal billing address (owned value object).</summary>
    public BillingAddress? BillingAddress { get; private set; }

    /// <summary>ISO 4217 alpha-3 currency code (always upper-case after construction).</summary>
    public string DefaultCurrency { get; private set; } = string.Empty;

    /// <summary>IANA timezone (defaults to <c>"UTC"</c>).</summary>
    public string Timezone { get; private set; } = "UTC";

    /// <summary>Lifecycle status.</summary>
    public CustomerStatus Status { get; private set; }

    /// <summary>External provider mappings (Stripe customer ID, Odoo partner ID, …).</summary>
    public IReadOnlyList<CustomerExternalMapping> ExternalMappings => _externalMappings.AsReadOnly();

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <summary>Explicit interface implementation: lets the audit/multi-tenant interceptor
    /// assign the tenant during materialisation while keeping public access read-only.</summary>
    Guid? IMultiTenant.TenantId { get => TenantId; set => TenantId = value; }

    // ── Lifecycle transitions (idempotent) ─────────────────────────────

    /// <summary>Reactivates a suspended customer. Idempotent: no-op if already active.</summary>
    /// <returns><c>true</c> when the state actually transitioned, <c>false</c> otherwise.</returns>
    public bool Activate()
    {
        if (Status == CustomerStatus.Active)
        {
            return false;
        }

        if (Status == CustomerStatus.Archived)
        {
            throw new InvalidOperationException(
                $"Customer '{Id}' is Archived. Archived customers cannot be reactivated.");
        }

        Status = CustomerStatus.Active;
        AddDomainEvent(new CustomerActivatedEvent(CustomerId.Create(Id), TenantId));
        AddDistributedEvent(new CustomerActivatedEto(CustomerId.Create(Id), TenantId));
        return true;
    }

    /// <summary>Suspends an active customer. Idempotent: no-op if already suspended.</summary>
    /// <param name="reason">Optional free-text justification (e.g., <c>"unpaid balance"</c>).</param>
    /// <returns><c>true</c> when the state actually transitioned, <c>false</c> otherwise.</returns>
    public bool Suspend(string? reason = null)
    {
        if (Status == CustomerStatus.Suspended)
        {
            return false;
        }

        if (Status == CustomerStatus.Archived)
        {
            throw new InvalidOperationException(
                $"Customer '{Id}' is Archived. Archived customers cannot be suspended.");
        }

        Status = CustomerStatus.Suspended;
        AddDomainEvent(new CustomerSuspendedEvent(CustomerId.Create(Id), TenantId, reason));
        AddDistributedEvent(new CustomerSuspendedEto(CustomerId.Create(Id), TenantId, reason));
        return true;
    }

    /// <summary>Archives the customer (terminal state). Idempotent: no-op if already archived.</summary>
    /// <returns><c>true</c> when the state actually transitioned, <c>false</c> otherwise.</returns>
    public bool Archive()
    {
        if (Status == CustomerStatus.Archived)
        {
            return false;
        }

        Status = CustomerStatus.Archived;
        AddDomainEvent(new CustomerArchivedEvent(CustomerId.Create(Id), TenantId));
        AddDistributedEvent(new CustomerArchivedEto(CustomerId.Create(Id), TenantId));
        return true;
    }

    // ── Identity updates ───────────────────────────────────────────────

    /// <summary>Updates the customer's contact identity (legal name, email, timezone).</summary>
    /// <remarks>
    /// Currency is intentionally not editable here — currency changes require migrating
    /// historical balances and are surfaced via a dedicated operation (out of scope for
    /// this aggregate's MVP).
    /// </remarks>
    public void UpdateContact(string legalName, string? email = null, string? timezone = null)
    {
        EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(legalName);

        LegalName = legalName;
        Email = email;
        Timezone = string.IsNullOrWhiteSpace(timezone) ? Timezone : timezone;

        AddDomainEvent(new CustomerUpdatedEvent(CustomerId.Create(Id), TenantId));
        AddDistributedEvent(new CustomerUpdatedEto(CustomerId.Create(Id), TenantId));
    }

    /// <summary>Updates or clears the billing address.</summary>
    public void UpdateBillingAddress(BillingAddress? billingAddress)
    {
        EnsureMutable();
        BillingAddress = billingAddress;
        AddDomainEvent(new CustomerUpdatedEvent(CustomerId.Create(Id), TenantId));
        AddDistributedEvent(new CustomerUpdatedEto(CustomerId.Create(Id), TenantId));
    }

    // ── External mappings ──────────────────────────────────────────────

    /// <summary>Registers an external provider identifier against this customer.</summary>
    /// <exception cref="InvalidOperationException">A mapping for <paramref name="providerName"/> already exists.</exception>
    public void AddExternalMapping(Guid mappingId, string providerName, string externalId)
    {
        EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);

        if (_externalMappings.Any(m =>
            string.Equals(m.ProviderName, providerName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Customer '{Id}' already has an external mapping for provider '{providerName}'. "
                + "Remove the existing one before registering a new identifier.");
        }

        _externalMappings.Add(CustomerExternalMapping.Create(mappingId, providerName, externalId));

        AddDomainEvent(new CustomerExternalMappingAddedEvent(
            CustomerId.Create(Id), TenantId, providerName, externalId));
        AddDistributedEvent(new CustomerExternalMappingAddedEto(
            CustomerId.Create(Id), TenantId, providerName, externalId));
    }

    /// <summary>Removes the mapping for the given provider, if any.</summary>
    /// <returns><c>true</c> if a mapping was removed, <c>false</c> if none matched.</returns>
    public bool RemoveExternalMapping(string providerName)
    {
        EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        CustomerExternalMapping? existing = _externalMappings.FirstOrDefault(m =>
            string.Equals(m.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            return false;
        }

        _externalMappings.Remove(existing);
        return true;
    }

    /// <summary>Looks up the external identifier for a given provider, if registered.</summary>
    public string? FindExternalId(string providerName) =>
        _externalMappings.FirstOrDefault(m =>
            string.Equals(m.ProviderName, providerName, StringComparison.OrdinalIgnoreCase))?
            .ExternalId;

    private void EnsureMutable()
    {
        if (Status == CustomerStatus.Archived)
        {
            throw new InvalidOperationException(
                $"Customer '{Id}' is Archived. Archived customers are immutable.");
        }
    }
}
