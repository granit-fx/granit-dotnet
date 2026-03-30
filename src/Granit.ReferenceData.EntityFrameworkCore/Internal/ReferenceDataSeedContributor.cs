using Granit.Persistence.EntityFrameworkCore.DataSeeding;
using Granit.ReferenceData.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.ReferenceData.EntityFrameworkCore.Internal;

/// <summary>
/// Bridge between the <see cref="IReferenceDataSeeder{TEntity}"/> typed seeders and the
/// <see cref="IDataSeedContributor"/> infrastructure from <c>Granit.Persistence</c>.
/// </summary>
/// <remarks>
/// <para>
/// This contributor is registered once via
/// <c>AddReferenceDataStore&lt;TEntity, TDbContext&gt;()</c>. It resolves all
/// <see cref="IReferenceDataSeeder{TEntity}"/> from DI, orders them by
/// <see cref="IReferenceDataSeeder{TEntity}.Order"/>, and executes them sequentially.
/// </para>
/// <para>
/// Errors from individual seeders are logged and do not prevent remaining seeders
/// from executing (resilient, same as <c>DataSeeder</c>).
/// </para>
/// </remarks>
/// <typeparam name="TEntity">The concrete reference data entity type.</typeparam>
internal sealed partial class ReferenceDataSeedContributor<TEntity>(
    IServiceProvider serviceProvider,
    ILogger<ReferenceDataSeedContributor<TEntity>> logger) : IDataSeedContributor
    where TEntity : ReferenceDataEntity
{
    /// <inheritdoc/>
    public async Task SeedAsync(DataSeedContext context, CancellationToken cancellationToken = default)
    {
        IEnumerable<IReferenceDataSeeder<TEntity>> seeders =
            serviceProvider.GetServices<IReferenceDataSeeder<TEntity>>();

        IReferenceDataStoreReader<TEntity> storeReader =
            serviceProvider.GetRequiredService<IReferenceDataStoreReader<TEntity>>();
        IReferenceDataStoreWriter<TEntity> storeWriter =
            serviceProvider.GetRequiredService<IReferenceDataStoreWriter<TEntity>>();

        foreach (IReferenceDataSeeder<TEntity> seeder in seeders.OrderBy(s => s.Order))
        {
            string seederName = seeder.GetType().FullName ?? seeder.GetType().Name;

            try
            {
                LogSeederStarted(seederName, typeof(TEntity).Name);
                await seeder.SeedAsync(storeReader, storeWriter, cancellationToken).ConfigureAwait(false);
                LogSeederCompleted(seederName, typeof(TEntity).Name);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogSeederFailed(seederName, typeof(TEntity).Name, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Executing reference data seeder '{SeederName}' for entity '{EntityName}'.")]
    private partial void LogSeederStarted(string seederName, string entityName);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Reference data seeder '{SeederName}' for entity '{EntityName}' completed successfully.")]
    private partial void LogSeederCompleted(string seederName, string entityName);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Reference data seeder '{SeederName}' for entity '{EntityName}' failed. Seeding continues with remaining seeders.")]
    private partial void LogSeederFailed(string seederName, string entityName, Exception ex);
}
