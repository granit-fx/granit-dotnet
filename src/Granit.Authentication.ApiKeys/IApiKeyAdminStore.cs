using Granit.Authentication.ApiKeys.Domain;

namespace Granit.Authentication.ApiKeys;

/// <summary>
/// Administrative persistence operations for API keys (CRUD, revocation, rotation).
/// Extends <see cref="IApiKeyStore"/> with write operations exposed by management endpoints.
/// </summary>
public interface IApiKeyAdminStore
{
    /// <summary>
    /// Finds an API key by its unique identifier.
    /// </summary>
    Task<ApiKeyEntry?> FindByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a new API key entry.
    /// </summary>
    Task CreateAsync(ApiKeyEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an API key as revoked by setting <see cref="ApiKeyEntry.RevokedAt"/>.
    /// </summary>
    Task<bool> RevokeAsync(Guid id, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the permissions and allowed CIDR ranges for an API key.
    /// </summary>
    Task<bool> UpdateScopesAsync(
        Guid id,
        List<string> permissions,
        List<string> allowedCidrs,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns active (non-revoked) API keys that are approaching expiration and have
    /// NOT been notified within the dedupe window. Used by the daily "expiring soon"
    /// scanner.
    /// </summary>
    /// <param name="now">Current instant — keys with <c>ExpiresAt &gt; now</c> only.</param>
    /// <param name="leadTimeWindowEnd">
    /// Upper bound — keys with <c>ExpiresAt &lt;= leadTimeWindowEnd</c> are returned.
    /// </param>
    /// <param name="dedupeBefore">
    /// Inclusive cutoff — keys whose <c>LastExpirationNotifiedAt &gt;= dedupeBefore</c>
    /// are excluded (already alerted recently).
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<ApiKeyEntry>> ListExpiringSoonAsync(
        DateTimeOffset now,
        DateTimeOffset leadTimeWindowEnd,
        DateTimeOffset dedupeBefore,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists pending domain / integration events captured on the given entry — used
    /// by the scanner after calling <see cref="ApiKeyEntry.MarkExpirationNotified"/> so
    /// the <c>ApiKeyExpiringSoonEto</c> reaches the Wolverine outbox alongside the
    /// updated <c>LastExpirationNotifiedAt</c> stamp in a single transaction.
    /// </summary>
    Task SaveAsync(ApiKeyEntry entry, CancellationToken cancellationToken = default);
}
