using Granit.Identity.Endpoints.Dtos;
using Granit.Identity.Models;
using Granit.IpGeolocation;
using Granit.UserSessions;

namespace Granit.Identity.Endpoints.Internal;

/// <summary>
/// Enriches identity session/device responses with geolocation and risk, and masks the IP for exposure —
/// keeping <see cref="IdentityResponseMapper"/> pure. Resolution and masking degrade gracefully to
/// <c>null</c> when no provider/verdict is available.
/// </summary>
internal static class IdentitySessionEnrichment
{
    public static async Task<List<IdentitySessionResponse>> EnrichSessionsAsync(
        IReadOnlyList<IdentitySession> sessions,
        string userId,
        IIpGeolocationResolver geoResolver,
        IUserSessionRiskStore riskStore,
        bool exposeRawIp,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, UserSessionRiskVerdict> verdicts = await riskStore
            .GetManyAsync(userId, [.. sessions.Select(s => s.SessionId)], cancellationToken)
            .ConfigureAwait(false);

        List<IdentitySessionResponse> result = [];
        foreach (IdentitySession session in sessions)
        {
            result.Add(await EnrichSessionAsync(session, geoResolver, verdicts, exposeRawIp, cancellationToken)
                .ConfigureAwait(false));
        }

        return result;
    }

    public static async Task<List<IdentityDeviceActivityResponse>> EnrichDevicesAsync(
        IReadOnlyList<IdentityDeviceActivity> devices,
        string userId,
        IIpGeolocationResolver geoResolver,
        IUserSessionRiskStore riskStore,
        bool exposeRawIp,
        CancellationToken cancellationToken)
    {
        List<IdentityDeviceActivityResponse> result = [];
        foreach (IdentityDeviceActivity device in devices)
        {
            GeoLocation? location = await geoResolver.ResolveAsync(device.IpAddress, cancellationToken)
                .ConfigureAwait(false);
            List<IdentitySessionResponse> sessions = await EnrichSessionsAsync(
                device.Sessions, userId, geoResolver, riskStore, exposeRawIp, cancellationToken)
                .ConfigureAwait(false);

            result.Add(IdentityResponseMapper.ToResponse(device) with
            {
                Location = location,
                IpAddress = exposeRawIp ? device.IpAddress : IpMasking.Mask(device.IpAddress),
                Sessions = sessions,
            });
        }

        return result;
    }

    private static async Task<IdentitySessionResponse> EnrichSessionAsync(
        IdentitySession session,
        IIpGeolocationResolver geoResolver,
        IReadOnlyDictionary<string, UserSessionRiskVerdict> verdicts,
        bool exposeRawIp,
        CancellationToken cancellationToken)
    {
        GeoLocation? location = await geoResolver.ResolveAsync(session.IpAddress, cancellationToken)
            .ConfigureAwait(false);
        UserSessionRiskLevel? riskLevel = verdicts.TryGetValue(session.SessionId, out UserSessionRiskVerdict? verdict)
            ? verdict.Level
            : null;

        return IdentityResponseMapper.ToResponse(session) with
        {
            Location = location,
            IpAddress = exposeRawIp ? session.IpAddress : IpMasking.Mask(session.IpAddress),
            RiskLevel = riskLevel,
        };
    }
}
