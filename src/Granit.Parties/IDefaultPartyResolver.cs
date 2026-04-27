using Granit.Parties.Domain;

namespace Granit.Parties;

/// <summary>
/// Resolves the default host-scoped <see cref="Party"/> that represents a given
/// tenant in the SaaS host's billing relationship.
/// </summary>
/// <remarks>
/// <para>
/// The link is the reverse mapping <c>ExternalMappings[ProviderName == "tenant"].ExternalId == tenantId</c>
/// (constant <see cref="PartyExternalProviderNames.Tenant"/>). A host-scoped contact
/// gets this mapping at tenant-provisioning time so downstream modules (Invoicing,
/// Subscriptions, Payments, Tax, CustomerBalance) can migrate from tenant-keyed
/// billing references to <c>PartyId</c> without losing the tenant association.
/// </para>
/// <para>
/// This resolver is the single seam used by Features 2–5 to bridge the legacy
/// <c>tenantId</c> column to the new <c>contactId</c> FK during migration and at runtime.
/// Centralising the lookup here means the convention (host-scoped + reserved provider
/// name) lives in exactly one place.
/// </para>
/// </remarks>
public interface IDefaultPartyResolver
{
    /// <summary>
    /// Returns the host-scoped contact representing <paramref name="tenantId"/>, or
    /// <c>null</c> when no such mapping exists yet. Always queries with the multi-tenant
    /// filter disabled so the lookup succeeds regardless of the active scope.
    /// </summary>
    Task<Party?> GetDefaultForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
