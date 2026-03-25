using Granit.BackgroundJobs.Domain;
namespace Granit.BackgroundJobs.Options;

/// <summary>
/// Configuration options for the Granit background jobs module.
/// Bound from the <c>"BackgroundJobs"</c> section of <c>appsettings.json</c>.
/// </summary>
/// <remarks>
/// In <see cref="JobStoreMode.InMemory"/> mode (default), no database is required —
/// ideal for development and tests. State is lost on restart.
/// <para>
/// In <see cref="JobStoreMode.Durable"/> mode, <see cref="ConnectionString"/> must point
/// to a SQL Server or PostgreSQL instance. The module creates and manages the
/// <c>background_jobs_background_jobs</c> table automatically.
/// </para>
/// </remarks>
public sealed class BackgroundJobsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "BackgroundJobs";

    /// <summary>
    /// Store mode: <see cref="JobStoreMode.InMemory"/> (default) or
    /// <see cref="JobStoreMode.Durable"/> (EF Core — SQL Server / PostgreSQL).
    /// </summary>
    public JobStoreMode Mode { get; set; } = JobStoreMode.InMemory;

    /// <summary>
    /// Connection string for the <c>BackgroundJobsDbContext</c>.
    /// Required when <see cref="Mode"/> is <see cref="JobStoreMode.Durable"/>.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
}
