using Granit.Authentication.ApiKeys.Domain;

namespace Granit.Authentication.ApiKeys.Endpoints.Dtos;

/// <summary>
/// API key details returned by list and get endpoints (never includes the raw secret).
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="Type">Key type.</param>
/// <param name="Environment">Target environment.</param>
/// <param name="Prefix">Key prefix for identification.</param>
/// <param name="LastFourChars">Last four characters for identification.</param>
/// <param name="Permissions">Granted permissions.</param>
/// <param name="AllowedCidrs">Allowed CIDR ranges.</param>
/// <param name="ExpiresAt">Expiration date, if set.</param>
/// <param name="LastUsedAt">Last usage timestamp.</param>
/// <param name="RevokedAt">Revocation timestamp, if revoked.</param>
/// <param name="CacheBehavior">Cache behavior.</param>
/// <param name="CreatedAt">Creation timestamp.</param>
/// <param name="ModifiedAt">Last modification timestamp; <c>null</c> until the key is first modified (e.g. scope update).</param>
public sealed record ApiKeyResponse(
    Guid Id,
    string Name,
    ApiKeyType Type,
    string Environment,
    string Prefix,
    string LastFourChars,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> AllowedCidrs,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt,
    CacheBehavior CacheBehavior,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt)
{
    /// <summary>Maps an <see cref="ApiKeyEntry"/> to a response DTO.</summary>
    internal static ApiKeyResponse FromEntry(ApiKeyEntry entry) =>
        new(entry.Id,
            entry.Name,
            entry.Type,
            entry.Environment,
            entry.Prefix,
            entry.LastFourChars,
            entry.Permissions,
            entry.AllowedCidrs,
            entry.ExpiresAt,
            entry.LastUsedAt,
            entry.RevokedAt,
            entry.CacheBehavior,
            entry.CreatedAt,
            entry.ModifiedAt);
}
