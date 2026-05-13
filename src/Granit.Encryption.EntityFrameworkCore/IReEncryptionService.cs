namespace Granit.Encryption.EntityFrameworkCore;

/// <summary>
/// Iterates entities that have <see cref="EncryptedAttribute"/> properties
/// and forces re-encryption to the current key version.
/// </summary>
/// <remarks>
/// Call this service after a key rotation to ensure all rows are encrypted with
/// the current key version. The operation is idempotent and safe to run multiple times.
/// Invoke on-demand (e.g. from an admin endpoint, a CLI command, or an app-defined
/// background job) — this is not itself a scheduled Granit background job.
/// </remarks>
public interface IReEncryptionService
{
    /// <summary>
    /// Re-encrypts all <see cref="EncryptedAttribute"/> properties of
    /// <typeparamref name="TEntity"/> in configurable batches.
    /// </summary>
    /// <typeparam name="TEntity">Entity type registered in the DbContext.</typeparam>
    /// <param name="batchSize">Number of entities to process per batch. Defaults to 500.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task ReEncryptAsync<TEntity>(int batchSize = 500, CancellationToken cancellationToken = default)
        where TEntity : class;
}
