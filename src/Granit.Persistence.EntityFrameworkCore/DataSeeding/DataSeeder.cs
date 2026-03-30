using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Granit.Persistence.EntityFrameworkCore.DataSeeding;

/// <summary>
/// Default implementation of <see cref="IDataSeeder"/>.
/// Resolves all <see cref="IDataSeedContributor"/> from a scoped service provider
/// and executes them sequentially. Errors are logged but do not stop remaining contributors.
/// </summary>
internal sealed partial class DataSeeder(
    IServiceScopeFactory scopeFactory,
    ILogger<DataSeeder> logger) : IDataSeeder
{
    /// <inheritdoc/>
    public async Task SeedAsync(DataSeedContext context, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        IEnumerable<IDataSeedContributor> contributors =
            scope.ServiceProvider.GetServices<IDataSeedContributor>();

        foreach (IDataSeedContributor contributor in contributors)
        {
            string contributorName = contributor.GetType().FullName ?? contributor.GetType().Name;

            try
            {
                LogContributorStarted(contributorName);
                await contributor.SeedAsync(context, cancellationToken).ConfigureAwait(false);
                LogContributorCompleted(contributorName);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogContributorFailed(contributorName, ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Executing data seed contributor '{ContributorName}'.")]
    private partial void LogContributorStarted(string contributorName);

    [LoggerMessage(Level = LogLevel.Debug,
        Message = "Data seed contributor '{ContributorName}' completed successfully.")]
    private partial void LogContributorCompleted(string contributorName);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Data seed contributor '{ContributorName}' failed. Seeding continues with remaining contributors.")]
    private partial void LogContributorFailed(string contributorName, Exception ex);
}
