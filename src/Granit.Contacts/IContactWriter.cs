using Granit.Contacts.Domain;

namespace Granit.Contacts;

/// <summary>Writer-side abstraction for the <see cref="Contact"/> aggregate (CQRS write).</summary>
public interface IContactWriter
{
    /// <summary>Persists a new contact.</summary>
    Task AddAsync(Contact contact, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing contact.</summary>
    Task UpdateAsync(Contact contact, CancellationToken cancellationToken = default);
}
