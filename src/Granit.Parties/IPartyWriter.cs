using Granit.Parties.Domain;

namespace Granit.Parties;

/// <summary>Writer-side abstraction for the <see cref="Party"/> aggregate (CQRS write).</summary>
public interface IPartyWriter
{
    /// <summary>Persists a new party.</summary>
    Task AddAsync(Party party, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing party.</summary>
    Task UpdateAsync(Party party, CancellationToken cancellationToken = default);
}
