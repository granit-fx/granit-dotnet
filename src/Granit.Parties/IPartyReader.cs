using Granit.Parties.Domain;
using Granit.Parties.Domain.ValueObjects;

namespace Granit.Parties;

/// <summary>Reader-side abstraction for the <see cref="Party"/> aggregate (CQRS read).</summary>
/// <remarks>
/// All lookups honour the active tenant scope via the multi-tenant query filter — host-scoped
/// contacts are visible only when no tenant context is active (or when the filter is bypassed
/// via <c>IDataFilter.Disable&lt;IMultiTenant&gt;()</c>).
/// </remarks>
public interface IPartyReader
{
    /// <summary>Returns the contact with the given identifier, or <c>null</c>.</summary>
    Task<Party?> GetByIdAsync(PartyId id, CancellationToken cancellationToken = default);

    /// <summary>Returns the contact carrying the given external mapping, or <c>null</c>.</summary>
    Task<Party?> GetByExternalIdAsync(
        string providerName, string externalId, CancellationToken cancellationToken = default);

    /// <summary>Returns the contact linked to the given authenticated user, or <c>null</c>.</summary>
    Task<Party?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Returns all contacts in the active scope.</summary>
    Task<IReadOnlyList<Party>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns contacts whose role set includes any of the given role flags.</summary>
    Task<IReadOnlyList<Party>> ListByRoleAsync(
        PartyRoles role, CancellationToken cancellationToken = default);
}
