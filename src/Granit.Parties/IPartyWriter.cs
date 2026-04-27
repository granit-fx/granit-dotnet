using Granit.Parties.Domain;

namespace Granit.Parties;

/// <summary>Writer-side abstraction for the <see cref="Party"/> aggregate (CQRS write).</summary>
public interface IPartyWriter
{
    /// <summary>Persists a new contact.</summary>
    Task AddAsync(Party contact, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing contact.</summary>
    Task UpdateAsync(Party contact, CancellationToken cancellationToken = default);
}
