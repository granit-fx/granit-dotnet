using Granit.Domain;
using Granit.MultiTenancy.Events;

namespace Granit.MultiTenancy.Domain;

/// <summary>
/// Host-level tenant aggregate root.
/// Tenants are NOT themselves multi-tenant (<c>IMultiTenant</c>) — they are managed
/// at the host level by platform administrators.
/// </summary>
/// <remarks>
/// All state transitions go through behavior methods that enforce invariants
/// and raise domain events dispatched by <c>DomainEventDispatcherInterceptor</c>.
/// </remarks>
public sealed class Tenant : FullAuditedAggregateRoot, ITenantInfo
{
    /// <summary>Display name of the tenant (max 256 characters).</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Unique slug/subdomain identifier (max 64 characters, lowercase alphanumeric + hyphens).</summary>
    public string Identifier { get; private set; } = string.Empty;

    /// <summary>Optional contact email address (max 256 characters).</summary>
    public string? ContactEmail { get; private set; }

    /// <inheritdoc/>
    public string? Jurisdiction { get; private set; }

    /// <summary>Whether the tenant is active and can be resolved by the middleware.</summary>
    public bool Activated { get; private set; } = true;

    /// <summary>
    /// Optional custom domain for this tenant (e.g., <c>"app.acme-corp.com"</c>).
    /// When set, outbound URLs (emails, templates) use this domain instead of
    /// the subdomain derived from <see cref="Identifier"/>.
    /// Max 253 characters (RFC 1035).
    /// </summary>
    public string? CustomDomain { get; private set; }

    // Explicit interface: Entity.Id is Guid, ITenantInfo.Id is Guid?
    Guid? ITenantInfo.Id => Id;

    /// <summary>
    /// Private constructor for EF Core materialization.
    /// </summary>
    private Tenant() { }

    /// <summary>
    /// Creates a new active tenant.
    /// </summary>
    /// <param name="id">Unique identifier.</param>
    /// <param name="name">Display name (max 256 characters).</param>
    /// <param name="identifier">Unique slug/subdomain identifier (max 64 characters).</param>
    /// <param name="contactEmail">Optional contact email address.</param>
    /// <param name="jurisdiction">Privacy regulation code or ISO country code (or <c>null</c>).</param>
    /// <returns>A new active <see cref="Tenant"/> instance.</returns>
    public static Tenant Create(
        Guid id,
        string name,
        string identifier,
        string? contactEmail = null,
        string? jurisdiction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        var tenant = new Tenant
        {
            Id = id,
            Name = name,
            Identifier = identifier,
            ContactEmail = contactEmail,
            Jurisdiction = jurisdiction,
            Activated = true,
        };

        tenant.AddDomainEvent(new TenantCreatedEvent(id, name, identifier));
        return tenant;
    }

    /// <summary>
    /// Updates the tenant's display name, contact email, and jurisdiction.
    /// </summary>
    /// <param name="name">New display name.</param>
    /// <param name="contactEmail">New contact email (or <c>null</c> to clear).</param>
    /// <param name="jurisdiction">Privacy regulation code or ISO country code (or <c>null</c>).</param>
    public void UpdateDetails(string name, string? contactEmail, string? jurisdiction)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Name = name;
        ContactEmail = contactEmail;
        Jurisdiction = jurisdiction;

        AddDomainEvent(new TenantUpdatedEvent(Id, name, contactEmail));
    }

    /// <summary>
    /// Sets or clears the custom domain for this tenant.
    /// Raises <see cref="TenantCustomDomainChangedEvent"/> for cache invalidation.
    /// </summary>
    /// <param name="customDomain">
    /// The custom domain (e.g., <c>"app.acme-corp.com"</c>), or <c>null</c> to clear.
    /// </param>
    public void SetCustomDomain(string? customDomain)
    {
        string? normalized = string.IsNullOrWhiteSpace(customDomain) ? null : customDomain.Trim().ToLowerInvariant();

        if (string.Equals(CustomDomain, normalized, StringComparison.Ordinal))
        {
            return;
        }

        string? oldDomain = CustomDomain;
        CustomDomain = normalized;
        AddDomainEvent(new TenantCustomDomainChangedEvent(Id, oldDomain, normalized));
    }

    /// <summary>
    /// Activates the tenant, allowing it to be resolved by the middleware.
    /// </summary>
    public void Activate()
    {
        if (Activated)
        {
            return;
        }

        Activated = true;
        AddDomainEvent(new TenantActivatedEvent(Id));
    }

    /// <summary>
    /// Deactivates the tenant, preventing it from being resolved by the middleware.
    /// Can trigger session invalidation via <see cref="TenantDeactivatedEvent"/> handlers.
    /// </summary>
    public void Deactivate()
    {
        if (!Activated)
        {
            return;
        }

        Activated = false;
        AddDomainEvent(new TenantDeactivatedEvent(Id));
    }
}
