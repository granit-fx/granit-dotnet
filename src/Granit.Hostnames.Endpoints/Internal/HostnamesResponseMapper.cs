using Granit.Hostnames.Domain;
using Granit.Hostnames.Endpoints.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Granit.Hostnames.Endpoints.Internal;

internal static class HostnamesResponseMapper
{
    internal static ManagedHostnameResponse ToResponse(ManagedHostname h) =>
        new(h.Id,
            h.Host.Value,
            h.OwnerType,
            h.OwnerId,
            h.TenantId,
            h.IsPrimary,
            h.Status.ToString(),
            h.VerificationToken,
            h.ExpectedDnsRecords,
            h.LastCheckedAt,
            h.Conflicts,
            h.FailedCheckCount,
            h.NextCheckAt,
            h.CertificateStatus.ToString(),
            h.CertExpiresAt,
            h.CreatedAt,
            h.CreatedBy,
            h.ModifiedAt,
            h.ModifiedBy,
            h.ConcurrencyStamp);

    internal static ProblemHttpResult HostnameNotFound(Guid id) =>
        TypedResults.Problem(
            detail: $"No managed hostname with id '{id}' was found.",
            statusCode: StatusCodes.Status404NotFound);
}
