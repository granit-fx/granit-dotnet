using Granit.Parties.Domain;

namespace Granit.Parties;

/// <summary>
/// Seeds a host-scoped <see cref="Party"/> that represents a tenant in the SaaS host's
/// billing relationship. Mirrors <see cref="IDefaultPartyResolver"/>: the resolver reads
/// the tenant ↔ contact link, the seeder creates it.
/// </summary>
/// <remarks>
/// <para>
/// Called at tenant-provisioning time (typically by a Wolverine handler subscribing to
/// <c>Granit.MultiTenancy.Events.TenantCreatedEvent</c> in
/// <c>Granit.Parties.MultiTenancy</c>) so that downstream modules — Invoicing,
/// Subscriptions, Payments — can resolve a billing identity for the new tenant
/// immediately, without manual admin intervention.
/// </para>
/// <para>
/// Idempotent: calling <see cref="SeedForTenantAsync"/> a second time for the same
/// <paramref name="tenantId"/> returns the existing contact instead of creating a
/// duplicate. Wolverine's at-least-once delivery is therefore safe.
/// </para>
/// </remarks>
public interface IDefaultPartySeeder
{
    /// <summary>
    /// Returns the existing host-scoped <see cref="Party"/> for <paramref name="tenantId"/>,
    /// or creates a fresh one with the canonical
    /// <see cref="PartyExternalProviderNames.Tenant"/> external mapping when none exists.
    /// </summary>
    /// <param name="tenantId">Identifier of the tenant being provisioned.</param>
    /// <param name="tenantName">Display name for the new contact (typically the tenant's name).</param>
    /// <param name="defaultCurrency">ISO 4217 currency code. Defaults to <c>"EUR"</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<Party> SeedForTenantAsync(
        Guid tenantId,
        string tenantName,
        string defaultCurrency = "EUR",
        CancellationToken cancellationToken = default);
}
