using Granit.BackgroundJobs.Options;
namespace Granit.BackgroundJobs.Domain;

/// <summary>
/// Determines the persistence strategy for the background jobs administrative store.
/// </summary>
public enum JobStoreMode
{
    /// <summary>
    /// Jobs are stored in a <c>ConcurrentDictionary</c> in memory.
    /// No database required. State is lost on application restart.
    /// Suitable for development and integration tests.
    /// </summary>
    InMemory = 0,

    /// <summary>
    /// Jobs are persisted in a relational database via EF Core.
    /// Supports SQL Server and PostgreSQL (GDPR/ISO 27001 compliant).
    /// Requires <see cref="BackgroundJobsOptions.ConnectionString"/>.
    /// </summary>
    Durable = 1,
}
