namespace Granit.Hostnames.Endpoints.Dtos;

/// <summary>
/// Request to register a new managed hostname.
/// </summary>
/// <param name="Host">
/// Fully-qualified domain name (e.g. <c>shop.acme.com</c>). Normalised to lower case.
/// Must be globally unique — duplicate registration is rejected with 409.
/// </param>
/// <param name="OwnerType">
/// Opaque owner-resource discriminator (e.g. <c>"cms.site"</c>).
/// </param>
/// <param name="OwnerId">Identifier of the owning resource.</param>
/// <param name="TenantId">Owning tenant; <c>null</c> for a global (host-level) hostname.</param>
/// <param name="IsPrimary">
/// Whether this hostname is the canonical one for the owner.
/// The owner may have at most one primary hostname; setting a new primary does not
/// automatically clear the previous one — use the set-primary endpoint explicitly.
/// </param>
public sealed record CreateManagedHostnameRequest(
    string Host,
    string OwnerType,
    Guid OwnerId,
    Guid? TenantId = null,
    bool IsPrimary = false);
