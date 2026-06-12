using System.Runtime.CompilerServices;
using Granit.Identity.Federated.Domain;
using Granit.MultiTenancy;
using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Fragments;

namespace Granit.Identity.Federated.Privacy.DataExport;

/// <summary>
/// Privacy data provider for <c>Granit.Identity.Federated</c>. Exports the user's local
/// federated cache entry (profile mirror + sync metadata) as a single staged JSON fragment
/// during the scatter-gather export saga (GDPR Art. 15 / 20).
/// </summary>
/// <remarks>
/// Federated providers (Keycloak, Entra ID, Cognito, Google Cloud) typically emit a
/// GUID-format <c>sub</c> claim — we round-trip the supplied user id as the string
/// representation to match <see cref="FederatedIdentity.ExternalUserId"/>. When the user is
/// not cached locally (authenticated once and never exercised a feature that populated the
/// cache), the provider yields nothing.
/// </remarks>
public sealed class IdentityFederatedPrivacyDataProvider(
    IFederatedUserCacheReader cacheReader,
    ICurrentTenant currentTenant,
    IStagedFragmentBuilder fragmentBuilder) : IPrivacyDataProvider
{
    /// <inheritdoc />
    public static string ProviderName => "identity-federated";

    /// <inheritdoc />
    public static string DisplayKey => "Privacy.Scopes.IdentityFederated";

    /// <inheritdoc />
    public static string? FeatureName => null;

    /// <inheritdoc />
    public async ValueTask<bool> HasDataAsync(PrivacyExportContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        string externalUserId = context.SubjectUserId.ToString();
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;
        FederatedIdentity? entry = await cacheReader
            .FindByExternalIdAsync(externalUserId, tenantId, cancellationToken)
            .ConfigureAwait(false);
        return entry is not null;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<ExportFragment> ExportAsync(
        PrivacyExportContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ExportCoreAsync(context, cancellationToken);
    }

    private async IAsyncEnumerable<ExportFragment> ExportCoreAsync(
        PrivacyExportContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string externalUserId = context.SubjectUserId.ToString();
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        FederatedIdentity? entry = await cacheReader
            .FindByExternalIdAsync(externalUserId, tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (entry is null)
        {
            yield break;
        }

        var dto = new IdentityFederatedExportResponse(
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

        yield return await fragmentBuilder
            .BuildJsonAsync(context, ProviderName, "identity-federated.json", dto, cancellationToken)
            .ConfigureAwait(false);
    }
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
