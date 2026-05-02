using System.Text.Json;
using Granit.Identity.Federated.Domain;
using Granit.MultiTenancy;
using Granit.Privacy.DataExport;

namespace Granit.Identity.Federated.Privacy.DataExport;

/// <summary>
/// Privacy data provider for <c>Granit.Identity.Federated</c>. Exports the user's local
/// federated cache entry (profile mirror + sync metadata) as a JSON fragment during the
/// scatter-gather export saga (GDPR Art. 15).
/// </summary>
/// <remarks>
/// Federated providers (Keycloak, Entra ID, Cognito, Google Cloud) typically emit a
/// GUID-format <c>sub</c> claim — we round-trip <paramref name="userId"/> as the string
/// representation to match <see cref="FederatedIdentity.ExternalUserId"/>. When the user is
/// not cached locally (e.g. they authenticated once and never exercised a feature that
/// populated the cache), the provider returns <see cref="ReadOnlyMemory{T}.Empty"/>.
/// </remarks>
public sealed class IdentityFederatedPrivacyDataProvider(
    IFederatedUserCacheReader cacheReader,
    ICurrentTenant currentTenant) : IPrivacyDataProvider
{
    /// <inheritdoc />
    public static string ProviderName => "identity-federated";

    /// <inheritdoc />
    public static string ContentType => "application/json";

    /// <inheritdoc />
    public static string FileName(Guid requestId) => "identity-federated.json";

    /// <inheritdoc />
    public async Task<ReadOnlyMemory<byte>> ExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        string externalUserId = userId.ToString();
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        FederatedIdentity? entry = await cacheReader
            .FindByExternalIdAsync(externalUserId, tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (entry is null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        IdentityFederatedExportResponse dto = new(
            CacheEntryId: entry.Id,
            ExternalUserId: entry.ExternalUserId,
            Username: entry.Username,
            Email: entry.Email,
            FirstName: entry.FirstName,
            LastName: entry.LastName,
            Enabled: entry.Enabled,
            LastSyncedAt: entry.LastSyncedAt,
            TenantId: entry.TenantId,
            CreatedAt: entry.CreatedAt,
            ModifiedAt: entry.ModifiedAt,
            MetadataJson: entry.MetadataJson);

        return JsonSerializer.SerializeToUtf8Bytes(dto, ExportJsonOptions);
    }

    private static readonly JsonSerializerOptions ExportJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };
}

internal sealed record IdentityFederatedExportResponse(
    Guid CacheEntryId,
    string ExternalUserId,
    string? Username,
    string? Email,
    string? FirstName,
    string? LastName,
    bool Enabled,
    DateTimeOffset LastSyncedAt,
    Guid? TenantId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt,
    string? MetadataJson);
