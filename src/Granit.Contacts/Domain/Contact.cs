using Granit.Contacts.Domain.ValueObjects;
using Granit.Contacts.Events;
using Granit.DataProtection;
using Granit.Domain;

namespace Granit.Contacts.Domain;

/// <summary>
/// Generic party-management aggregate (Odoo <c>res.partner</c>-style). A contact represents
/// any natural person, legal entity, or organisational unit that the platform interacts
/// with — customers, suppliers, employees, leads, and combinations thereof.
/// </summary>
/// <remarks>
/// <para><b>Dual-use design.</b> Contact is <see cref="IMultiTenant"/>:</para>
/// <list type="bullet">
/// <item><c>TenantId == null</c> ⇒ <i>host-scoped</i> (the SaaS host's contacts: tenants-as-customers, vendors, internal staff).</item>
/// <item><c>TenantId == &lt;tenant&gt;</c> ⇒ <i>tenant-scoped</i> (a tenant's e-commerce / CRM / procurement contacts).</item>
/// </list>
/// <para><b>Multi-role.</b> A single contact can simultaneously hold any combination of
/// <see cref="ContactRoles"/> flags (Customer + Supplier for a reseller you both buy from
/// and sell to, Customer + Employee for staff who consume the product, etc.). Roles are
/// queried via <see cref="HasRole"/> and added/removed individually.</para>
/// <para><b>Hierarchy.</b> A contact may be attached to a parent via <see cref="ParentContactId"/>
/// — typical use cases: a Person belongs to a Company, a Department reports to a parent
/// Company, a subsidiary is owned by a holding. Same-tenant invariant is enforced at attach
/// time. Cycle detection is deferred to a later iteration.</para>
/// <para><b>User linkage.</b> An Individual contact may be linked to an authenticated user
/// via <see cref="UserId"/> — used by self-service portals to surface "my profile". One user
/// may link to at most one contact (uniqueness enforced by the EF configuration).</para>
/// <para><b>Lifecycle.</b> <see cref="ContactStatus.Active"/> ↔ <see cref="ContactStatus.Suspended"/>;
/// either may transition to terminal <see cref="ContactStatus.Archived"/>. Archived
/// contacts are immutable and cannot be reactivated.</para>
/// <para><b>External mappings.</b> Polyglot — at most one mapping per provider, enforced
/// defensively at the aggregate and by a unique index in the EF configuration.</para>
/// </remarks>
public sealed class Contact : AuditedAggregateRoot, IMultiTenant
{
    private readonly List<ContactExternalMapping> _externalMappings = [];

    private Contact() { }

    /// <summary>Creates a new contact in <see cref="ContactStatus.Active"/> status.</summary>
    /// <param name="id">Unique identifier.</param>
    /// <param name="tenantId">Owning tenant identifier; <c>null</c> for host-scoped contacts.</param>
    /// <param name="kind">Whether the contact is an individual, a company, or a department.</param>
    /// <param name="name">Display / legal name (required, max 256 chars).</param>
    /// <param name="defaultCurrency">ISO 4217 alpha-3 currency code (required, exactly 3 chars).</param>
    /// <param name="roles">Initial role set (defaults to <see cref="ContactRoles.Customer"/>).</param>
    /// <param name="email">Primary contact email (optional).</param>
    /// <param name="phone">Optional landline / generic phone.</param>
    /// <param name="mobilePhone">Optional mobile phone.</param>
    /// <param name="website">Optional website URL.</param>
    /// <param name="language">Optional ISO locale (e.g. <c>"fr-BE"</c>).</param>
    /// <param name="timezone">IANA timezone (defaults to <c>"UTC"</c>).</param>
    /// <param name="taxId">International VAT identifier (e.g. <c>"BE0123456789"</c>).</param>
    /// <param name="registrationNumber">Company registration number (BCE/KBO, SIRET, HRB, …).</param>
    /// <param name="address">Optional postal address (owned).</param>
    public static Contact Create(
        Guid id,
        Guid? tenantId,
        ContactKind kind,
        string name,
        string defaultCurrency,
        ContactRoles roles = ContactRoles.Customer,
        string? email = null,
        string? phone = null,
        string? mobilePhone = null,
        string? website = null,
        string? language = null,
        string? timezone = null,
        string? taxId = null,
        string? registrationNumber = null,
        Address? address = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultCurrency);
        if (defaultCurrency.Length != 3)
        {
            throw new ArgumentException(
                "DefaultCurrency must be a 3-letter ISO 4217 code.", nameof(defaultCurrency));
        }

        var contact = new Contact
        {
            Id = id,
            TenantId = tenantId,
            Kind = kind,
            Name = name,
            Email = email,
            Phone = phone,
            MobilePhone = mobilePhone,
            Website = website,
            Language = language,
            Timezone = string.IsNullOrWhiteSpace(timezone) ? "UTC" : timezone,
            DefaultCurrency = defaultCurrency.ToUpperInvariant(),
            TaxId = taxId,
            RegistrationNumber = registrationNumber,
            Address = address,
            Roles = roles,
            Status = ContactStatus.Active,
        };

        contact.AddDomainEvent(new ContactCreatedEvent(
            ContactId.Create(id), tenantId, kind, name, roles));
        contact.AddDistributedEvent(new ContactCreatedEto(
            ContactId.Create(id), tenantId, kind, name, roles, contact.DefaultCurrency));

        return contact;
    }

    // ── Identity ───────────────────────────────────────────────────

    /// <summary>Whether the contact is an individual, a company, or a department.</summary>
    public ContactKind Kind { get; private set; }

    /// <summary>Display / legal name.</summary>
    [SensitiveData(Level = Sensitivity.Internal)]
    public string Name { get; private set; } = string.Empty;

    /// <summary>Primary contact email.</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    public string? Email { get; private set; }

    /// <summary>Landline / generic phone number.</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    public string? Phone { get; private set; }

    /// <summary>Mobile phone number.</summary>
    [SensitiveData(Level = Sensitivity.Confidential)]
    public string? MobilePhone { get; private set; }

    /// <summary>Website URL.</summary>
    public string? Website { get; private set; }

    /// <summary>Preferred locale (ISO BCP 47, e.g. <c>"fr-BE"</c>).</summary>
    public string? Language { get; private set; }

    /// <summary>IANA timezone (defaults to <c>"UTC"</c>).</summary>
    public string Timezone { get; private set; } = "UTC";

    /// <summary>ISO 4217 alpha-3 currency code (always upper-case after construction).</summary>
    public string DefaultCurrency { get; private set; } = string.Empty;

    // ── Tax & legal identity (contact-level, not address-level) ───

    /// <summary>International VAT identifier (e.g. <c>"BE0123456789"</c>, <c>"FR12345678901"</c>).</summary>
    public string? TaxId { get; private set; }

    /// <summary>Company registration number (BCE/KBO, SIRET, HRB, Companies House, …).</summary>
    public string? RegistrationNumber { get; private set; }

    // ── Address ───────────────────────────────────────────────────

    /// <summary>Postal address (single owned VO; multiple addresses deferred to a future epic).</summary>
    public Address? Address { get; private set; }

    // ── Hierarchy ─────────────────────────────────────────────────

    /// <summary>Parent contact identifier (Person→Company, subsidiary→holding, …). Same-tenant only.</summary>
    public ContactId? ParentContactId { get; private set; }

    // ── User linkage ──────────────────────────────────────────────

    /// <summary>
    /// Authenticated user identifier this contact represents. Only meaningful for
    /// <see cref="ContactKind.Individual"/>. One user → at most one contact (enforced by
    /// a partial unique index in the EF configuration).
    /// </summary>
    public Guid? UserId { get; private set; }

    // ── Roles ─────────────────────────────────────────────────────

    /// <summary>Set of roles this contact plays (<see cref="ContactRoles"/> flags).</summary>
    public ContactRoles Roles { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────

    /// <summary>Lifecycle status.</summary>
    public ContactStatus Status { get; private set; }

    // ── External mappings ─────────────────────────────────────────

    /// <summary>External provider mappings (Stripe customer ID, Odoo partner ID, …).</summary>
    public IReadOnlyList<ContactExternalMapping> ExternalMappings => _externalMappings.AsReadOnly();

    // ── Multi-tenancy ─────────────────────────────────────────────

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <summary>Explicit interface implementation: lets the audit/multi-tenant interceptor
    /// assign the tenant during materialisation while keeping public access read-only.</summary>
    Guid? IMultiTenant.TenantId { get => TenantId; set => TenantId = value; }

    // ─────────────────────────────────────────────────────────────────
    // Lifecycle (idempotent transitions)
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Reactivates a suspended contact. Idempotent; throws on Archived.</summary>
    public bool Activate()
    {
        if (Status == ContactStatus.Active) { return false; }
        EnsureNotArchived(nameof(Activate));

        Status = ContactStatus.Active;
        AddDomainEvent(new ContactActivatedEvent(ContactId.Create(Id), TenantId));
        AddDistributedEvent(new ContactActivatedEto(ContactId.Create(Id), TenantId));
        return true;
    }

    /// <summary>Suspends an active contact. Idempotent; throws on Archived.</summary>
    public bool Suspend(string? reason = null)
    {
        if (Status == ContactStatus.Suspended) { return false; }
        EnsureNotArchived(nameof(Suspend));

        Status = ContactStatus.Suspended;
        AddDomainEvent(new ContactSuspendedEvent(ContactId.Create(Id), TenantId, reason));
        AddDistributedEvent(new ContactSuspendedEto(ContactId.Create(Id), TenantId, reason));
        return true;
    }

    /// <summary>Archives the contact (terminal state). Idempotent.</summary>
    public bool Archive()
    {
        if (Status == ContactStatus.Archived) { return false; }

        Status = ContactStatus.Archived;
        AddDomainEvent(new ContactArchivedEvent(ContactId.Create(Id), TenantId));
        AddDistributedEvent(new ContactArchivedEto(ContactId.Create(Id), TenantId));
        return true;
    }

    // ─────────────────────────────────────────────────────────────────
    // Identity & address updates
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Updates the contact's identity fields (name, email, phones, website, locale, timezone).</summary>
    public void UpdateContact(
        string name,
        string? email = null,
        string? phone = null,
        string? mobilePhone = null,
        string? website = null,
        string? language = null,
        string? timezone = null)
    {
        EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        Email = email;
        Phone = phone;
        MobilePhone = mobilePhone;
        Website = website;
        Language = language;
        Timezone = string.IsNullOrWhiteSpace(timezone) ? Timezone : timezone;

        RaiseUpdated();
    }

    /// <summary>Updates the postal address (or clears it).</summary>
    public void UpdateAddress(Address? address)
    {
        EnsureMutable();
        Address = address;
        RaiseUpdated();
    }

    /// <summary>Updates the tax / legal identity fields.</summary>
    public void UpdateTaxIdentity(string? taxId, string? registrationNumber)
    {
        EnsureMutable();
        TaxId = taxId;
        RegistrationNumber = registrationNumber;
        RaiseUpdated();
    }

    // ─────────────────────────────────────────────────────────────────
    // Hierarchy
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Attaches this contact to a parent contact.</summary>
    /// <param name="parentContactId">The parent contact's identifier.</param>
    /// <param name="parentTenantId">The parent contact's <see cref="IMultiTenant.TenantId"/> for same-scope validation.</param>
    public void AttachToParent(ContactId parentContactId, Guid? parentTenantId)
    {
        EnsureMutable();
        ArgumentNullException.ThrowIfNull(parentContactId);

        if (parentContactId.Value == Id)
        {
            throw new InvalidOperationException("A contact cannot be its own parent.");
        }

        if (parentTenantId != TenantId)
        {
            throw new InvalidOperationException(
                "Parent contact must live in the same tenant scope (host or specific tenant).");
        }

        ParentContactId = parentContactId;
        AddDomainEvent(new ContactAttachedToParentEvent(
            ContactId.Create(Id), TenantId, parentContactId));
    }

    /// <summary>Detaches this contact from its parent. Idempotent.</summary>
    public bool DetachFromParent()
    {
        EnsureMutable();
        if (ParentContactId is null) { return false; }

        ContactId former = ParentContactId;
        ParentContactId = null;
        AddDomainEvent(new ContactDetachedFromParentEvent(
            ContactId.Create(Id), TenantId, former));
        return true;
    }

    // ─────────────────────────────────────────────────────────────────
    // User linkage
    // ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Links this contact to an authenticated user. Only valid for
    /// <see cref="ContactKind.Individual"/> contacts.
    /// </summary>
    public void LinkToUser(Guid userId)
    {
        EnsureMutable();
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User identifier must not be empty.", nameof(userId));
        }
        if (Kind != ContactKind.Individual)
        {
            throw new InvalidOperationException(
                $"Only Individual contacts can be linked to a user (this contact is {Kind}).");
        }

        UserId = userId;
        AddDomainEvent(new ContactLinkedToUserEvent(
            ContactId.Create(Id), TenantId, userId));
    }

    /// <summary>Clears the user linkage. Idempotent.</summary>
    public bool UnlinkFromUser()
    {
        EnsureMutable();
        if (UserId is null) { return false; }
        UserId = null;
        return true;
    }

    // ─────────────────────────────────────────────────────────────────
    // Roles
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Adds a role flag. Idempotent.</summary>
    public bool AddRole(ContactRoles role)
    {
        EnsureMutable();
        if (role == ContactRoles.None) { return false; }
        if ((Roles & role) == role) { return false; }

        Roles |= role;
        AddDomainEvent(new ContactRoleAddedEvent(ContactId.Create(Id), TenantId, role));
        return true;
    }

    /// <summary>Removes a role flag. Idempotent.</summary>
    public bool RemoveRole(ContactRoles role)
    {
        EnsureMutable();
        if (role == ContactRoles.None) { return false; }
        if ((Roles & role) == 0) { return false; }

        Roles &= ~role;
        AddDomainEvent(new ContactRoleRemovedEvent(ContactId.Create(Id), TenantId, role));
        return true;
    }

    /// <summary>Returns <c>true</c> if all flags in <paramref name="role"/> are set.</summary>
    public bool HasRole(ContactRoles role) => (Roles & role) == role;

    // ─────────────────────────────────────────────────────────────────
    // External mappings
    // ─────────────────────────────────────────────────────────────────

    /// <summary>Registers an external provider identifier against this contact.</summary>
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
                $"Contact '{Id}' already has an external mapping for provider '{providerName}'. "
                + "Remove the existing one before registering a new identifier.");
        }

        _externalMappings.Add(ContactExternalMapping.Create(mappingId, providerName, externalId));

        AddDomainEvent(new ContactExternalMappingAddedEvent(
            ContactId.Create(Id), TenantId, providerName, externalId));
        AddDistributedEvent(new ContactExternalMappingAddedEto(
            ContactId.Create(Id), TenantId, providerName, externalId));
    }

    /// <summary>Removes the mapping for the given provider, if any.</summary>
    public bool RemoveExternalMapping(string providerName)
    {
        EnsureMutable();
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);

        ContactExternalMapping? existing = _externalMappings.FirstOrDefault(m =>
            string.Equals(m.ProviderName, providerName, StringComparison.OrdinalIgnoreCase));

        if (existing is null) { return false; }

        _externalMappings.Remove(existing);
        return true;
    }

    /// <summary>Looks up the external identifier for a given provider, if registered.</summary>
    public string? FindExternalId(string providerName) =>
        _externalMappings.FirstOrDefault(m =>
            string.Equals(m.ProviderName, providerName, StringComparison.OrdinalIgnoreCase))?
            .ExternalId;

    // ─────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────

    private void EnsureMutable()
    {
        if (Status == ContactStatus.Archived)
        {
            throw new InvalidOperationException(
                $"Contact '{Id}' is Archived. Archived contacts are immutable.");
        }
    }

    private void EnsureNotArchived(string operation)
    {
        if (Status == ContactStatus.Archived)
        {
            throw new InvalidOperationException(
                $"Contact '{Id}' is Archived. {operation}() is not allowed.");
        }
    }

    private void RaiseUpdated()
    {
        AddDomainEvent(new ContactUpdatedEvent(ContactId.Create(Id), TenantId));
        AddDistributedEvent(new ContactUpdatedEto(ContactId.Create(Id), TenantId));
    }
}
