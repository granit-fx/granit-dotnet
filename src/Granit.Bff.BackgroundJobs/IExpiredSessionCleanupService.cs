namespace Granit.Bff.BackgroundJobs;

/// <summary>
/// Purges expired BFF sessions from the database.
/// </summary>
public interface IExpiredSessionCleanupService
{
    /// <summary>Deletes all sessions past their expiration time.</summary>
    Task ExecuteAsync(CancellationToken cancellationToken);
}
