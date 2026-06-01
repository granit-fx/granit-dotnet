namespace Granit.Hostnames.Endpoints.Dtos;

/// <summary>
/// Represents a managed hostname in API responses.
/// </summary>
/// <param name="Id">Stable identifier.</param>
/// <param name="Host">Fully-qualified domain name (normalised, lower case).</param>
/// <param name="OwnerType">Opaque owner-resource discriminator.</param>
/// <param name="OwnerId">Owning resource identifier.</param>
/// <param name="TenantId">Owning tenant; <c>null</c> for global hostnames.</param>
/// <param name="IsPrimary">Whether this is the owner's canonical hostname.</param>
/// <param name="Status">Lifecycle state string: <c>Pending</c>, <c>Verifying</c>, <c>Active</c>, or <c>Error</c>.</param>
/// <param name="CreatedAt">UTC timestamp when the hostname was registered.</param>
/// <param name="CreatedBy">Identity that registered the hostname.</param>
/// <param name="LastModifiedAt">UTC timestamp of the last change; <c>null</c> if never modified.</param>
/// <param name="LastModifiedBy">Identity that last modified the hostname; <c>null</c> if never modified.</param>
public sealed record ManagedHostnameResponse(
    Guid Id,
    string Host,
    string OwnerType,
    Guid OwnerId,
    Guid? TenantId,
    bool IsPrimary,
    string Status,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? LastModifiedAt,
    string? LastModifiedBy);
