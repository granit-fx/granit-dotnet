using Granit.CustomerBalance.Domain;

namespace Granit.CustomerBalance;

/// <summary>Persists balance account changes (command side of CQRS).</summary>
public interface IBalanceAccountWriter
{
    /// <summary>Persists a new balance account.</summary>
    Task AddAsync(BalanceAccount account, CancellationToken cancellationToken = default);

    /// <summary>Persists changes to an existing balance account.</summary>
    Task UpdateAsync(BalanceAccount account, CancellationToken cancellationToken = default);
}
