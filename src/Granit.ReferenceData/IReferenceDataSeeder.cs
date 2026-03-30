using Granit.ReferenceData.Domain;

namespace Granit.ReferenceData;

/// <summary>
/// Typed seeder for a specific reference data entity.
/// Implementations provide the initial data set (e.g., ISO 3166 countries from Nager.Country).
/// </summary>
/// <typeparam name="TEntity">The concrete reference data entity type.</typeparam>
/// <remarks>
/// <para>
/// Seeders are executed by the <c>ReferenceDataSeedContributor</c> bridge in
/// <c>Granit.ReferenceData.EntityFrameworkCore</c> which integrates with the
/// <c>IDataSeedContributor</c> infrastructure from <c>Granit.Persistence.EntityFrameworkCore</c>.
/// </para>
/// <para>
/// Implementations must be <b>idempotent</b>: running the seeder multiple times must
/// produce the same result (upsert by <see cref="ReferenceDataEntity.Code"/>).
/// </para>
/// <para>
/// Register implementations as transient:
/// <code>
/// services.AddTransient&lt;IReferenceDataSeeder&lt;Country&gt;, CountrySeeder&gt;();
/// </code>
/// </para>
/// </remarks>
public interface IReferenceDataSeeder<TEntity> where TEntity : ReferenceDataEntity
{
    /// <summary>
    /// Execution order. Seeders with lower values run first.
    /// Use this to control dependencies between reference data types
    /// (e.g., seed currencies before countries that reference them).
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Seeds the reference data using the provided store reader and writer.
    /// </summary>
    /// <param name="storeReader">The store reader to check existing entries.</param>
    /// <param name="storeWriter">The store writer to create or update entries.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SeedAsync(
        IReferenceDataStoreReader<TEntity> storeReader,
        IReferenceDataStoreWriter<TEntity> storeWriter,
        CancellationToken cancellationToken = default);
}
