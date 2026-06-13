namespace Granit.Bff.Endpoints.Internal;

/// <summary>
/// Masks a BFF session identifier for safe inclusion in logs and audit trails — the raw id is a
/// bearer-equivalent secret and must never appear in clear. Surfaces only the first and last four
/// characters of identifiers long enough to keep that non-reversible.
/// </summary>
internal static class BffSessionIdMasking
{
    internal static string Mask(string sessionId) =>
        sessionId.Length > 8 ? $"{sessionId[..4]}...{sessionId[^4..]}" : "****";
}
