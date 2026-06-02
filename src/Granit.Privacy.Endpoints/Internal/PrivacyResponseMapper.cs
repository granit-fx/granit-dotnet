using System.Net;
using System.Net.Sockets;
using Granit.Privacy.DataDeletion;
using Granit.Privacy.DataExport;
using Granit.Privacy.Endpoints.Dtos;
using Granit.Privacy.Regulations;
using Granit.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Granit.Privacy.Endpoints.Internal;

/// <summary>
/// Shared mapping helpers and authentication utilities used across Privacy endpoint files.
/// </summary>
internal static class PrivacyResponseMapper
{
    // -------------------------------------------------------------------------
    // Auth helpers
    // -------------------------------------------------------------------------

    internal static bool TryGetUserId(ICurrentUserService currentUser, out Guid userId)
    {
        userId = Guid.Empty;

        if (!currentUser.IsAuthenticated || currentUser.UserId is null)
        {
            return false;
        }

        return Guid.TryParse(currentUser.UserId, out userId);
    }

    internal static Guid? TryGetUserIdOrNull(HttpContext httpContext)
    {
        System.Security.Claims.Claim? sub = httpContext.User.FindFirst("sub")
            ?? httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);

        return sub is not null && Guid.TryParse(sub.Value, out Guid userId) ? userId : null;
    }

    internal static ProblemHttpResult UserNotAuthenticated() =>
        TypedResults.Problem(
            detail: "User is not authenticated or has no valid user ID.",
            statusCode: StatusCodes.Status401Unauthorized);

    // -------------------------------------------------------------------------
    // Response mappers
    // -------------------------------------------------------------------------

    internal static PrivacyExportStatusResponse MapExportStatus(ExportRequestStatus status) =>
        new(
            status.RequestId,
            status.State.ToString(),
            status.RequestedAt,
            status.CompletedAt,
            status.ArchiveBlobReferenceId,
            status.MissingProviders);

    internal static PrivacyDeletionStatusResponse MapDeletionStatus(DeletionRequestStatus status) =>
        new(
            status.RequestId,
            status.State.ToString(),
            status.Reason,
            status.RequestedAt,
            status.ScheduledDeletionAt,
            status.CancelledAt,
            status.ExecutedAt);

    // -------------------------------------------------------------------------
    // Regulation resolver helper
    // -------------------------------------------------------------------------

    internal static async Task<string> ResolveRegulationAsync(
        IPrivacyRegulationResolver? resolver,
        CancellationToken cancellationToken)
    {
        if (resolver is null)
        {
            return "EU_GDPR";
        }

        PrivacyRegulationProfile profile = await resolver.ResolveAsync(cancellationToken).ConfigureAwait(false);
        return profile.Regulation.Value;
    }

    // -------------------------------------------------------------------------
    // IP pseudonymization
    // -------------------------------------------------------------------------

    internal static string? PseudonymizeIpAddress(string? ipAddress)
    {
        if (string.IsNullOrEmpty(ipAddress))
        {
            return null;
        }

        if (!IPAddress.TryParse(ipAddress, out IPAddress? ip))
        {
            return null;
        }

        byte[] bytes = ip.GetAddressBytes();

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            // IPv4: mask to /16 — zero last 2 octets
            bytes[2] = 0;
            bytes[3] = 0;
            return new IPAddress(bytes).ToString();
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // IPv6: mask to /48 — zero last 10 bytes (80 bits)
            for (int i = 6; i < 16; i++)
            {
                bytes[i] = 0;
            }

            return new IPAddress(bytes).ToString();
        }

        return null;
    }
}
