using Granit.Contacts.Domain;
using Granit.Contacts.Domain.ValueObjects;

namespace Granit.Contacts;

/// <summary>Reader-side abstraction for the <see cref="Contact"/> aggregate (CQRS read).</summary>
/// <remarks>
/// All lookups honour the active tenant scope via the multi-tenant query filter — host-scoped
/// contacts are visible only when no tenant context is active (or when the filter is bypassed
/// via <c>IDataFilter.Disable&lt;IMultiTenant&gt;()</c>).
/// </remarks>
public interface IContactReader
{
    /// <summary>Returns the contact with the given identifier, or <c>null</c>.</summary>
    Task<Contact?> GetByIdAsync(ContactId id, CancellationToken cancellationToken = default);

    /// <summary>Returns the contact carrying the given external mapping, or <c>null</c>.</summary>
    Task<Contact?> GetByExternalIdAsync(
        string providerName, string externalId, CancellationToken cancellationToken = default);

    /// <summary>Returns the contact linked to the given authenticated user, or <c>null</c>.</summary>
    Task<Contact?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Returns all contacts in the active scope.</summary>
    Task<IReadOnlyList<Contact>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns contacts whose role set includes any of the given role flags.</summary>
    Task<IReadOnlyList<Contact>> ListByRoleAsync(
        ContactRoles role, CancellationToken cancellationToken = default);
}
