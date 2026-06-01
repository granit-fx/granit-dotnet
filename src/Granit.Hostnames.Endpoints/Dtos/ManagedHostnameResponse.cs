using Granit.Hostnames.Domain;

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
/// <param name="VerificationToken">TXT challenge token; <c>null</c> until verification starts.</param>
/// <param name="ExpectedDnsRecords">DNS records the owner must configure; empty until verification starts.</param>
/// <param name="LastCheckedAt">When the last DNS check ran; <c>null</c> before any check.</param>
/// <param name="Conflicts">DNS conflicts from the last check; empty when verified.</param>
/// <param name="FailedCheckCount">Consecutive DNS check failure count.</param>
/// <param name="NextCheckAt">When the poller will retry; <c>null</c> when dormant.</param>
/// <param name="CreatedAt">UTC timestamp when the hostname was registered.</param>
/// <param name="CreatedBy">Identity that registered the hostname.</param>
/// <param name="ModifiedAt">UTC timestamp of the last change; <c>null</c> if never modified.</param>
/// <param name="ModifiedBy">Identity that last modified the hostname; <c>null</c> if never modified.</param>
public sealed record ManagedHostnameResponse(
    Guid Id,
    string Host,
    string OwnerType,
    Guid OwnerId,
    Guid? TenantId,
    bool IsPrimary,
    string Status,
    string? VerificationToken,
    IReadOnlyList<ExpectedDnsRecord> ExpectedDnsRecords,
    DateTimeOffset? LastCheckedAt,
    IReadOnlyList<DnsConflict> Conflicts,
    int FailedCheckCount,
    DateTimeOffset? NextCheckAt,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    DateTimeOffset? ModifiedAt,
    string? ModifiedBy);
