namespace Granit.Authentication.ApiKeys.Dtos;

/// <summary>
/// Summary projection of an <see cref="Domain.ApiKeyEntry"/> for the list/query endpoint.
/// </summary>
/// <remarks>
/// Returned by the query engine when <c>MapGranitQuery&lt;ApiKeyEntry&gt;</c> is mounted with
/// <see cref="Queries.ApiKeyEntryQueryDefinition"/>. The raw secret (<c>HashedKey</c>) is never
/// projected. <c>Permissions</c> and <c>AllowedCidrs</c> are deliberately excluded from the list
/// view — they are detail-level data exposed only via the <c>GET /{id}</c> endpoint.
/// </remarks>
/// <param name="Id">Unique identifier.</param>
/// <param name="Name">Display name.</param>
/// <param name="Type">Key type.</param>
/// <param name="Environment">Target environment.</param>
/// <param name="Prefix">Key prefix for identification.</param>
/// <param name="LastFourChars">Last four characters for identification.</param>
/// <param name="ExpiresAt">Expiration date, if set.</param>
/// <param name="LastUsedAt">Last usage timestamp.</param>
/// <param name="RevokedAt">Revocation timestamp, if revoked.</param>
/// <param name="CacheBehavior">Cache behavior.</param>
/// <param name="CreatedAt">Creation timestamp.</param>
public sealed record ApiKeyListItemResponse(
    Guid Id,
    string Name,
    ApiKeyType Type,
    string Environment,
    string Prefix,
    string LastFourChars,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt,
    CacheBehavior CacheBehavior,
    DateTimeOffset CreatedAt);
