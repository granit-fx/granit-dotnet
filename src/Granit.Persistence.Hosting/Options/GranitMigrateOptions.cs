namespace Granit.Persistence.Hosting.Options;

/// <summary>
/// Configuration options for the Granit migration runner.
/// </summary>
public sealed class GranitMigrateOptions
{
    /// <summary>
    /// The CLI argument that triggers migration mode. Default: <c>"--migrate"</c>.
    /// </summary>
    /// <remarks>
    /// Only CLI arguments are supported — environment variables are intentionally excluded
    /// to prevent Kubernetes CrashLoopBackOff when accidentally set on a Deployment.
    /// </remarks>
    public string CliFlag { get; set; } = "--migrate";

    /// <summary>
    /// Whether to run data seeding after migrations complete. Default: <c>true</c>.
    /// </summary>
    public bool SeedAfterMigration { get; set; } = true;

    /// <summary>
    /// Timeout for the entire migration operation. Default: 5 minutes.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to run data seeding at normal startup (without <c>--migrate</c>). Default: <c>false</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When <c>true</c>, <see cref="Granit.Persistence.DataSeeding.DataSeedingHostedService"/>
    /// runs at startup. This is unsafe for multi-instance deployments (K8s) because all pods
    /// would seed concurrently, causing race conditions.
    /// </para>
    /// <para>
    /// Use <c>true</c> only for single-instance development environments.
    /// A warning is logged when this option is enabled.
    /// </para>
    /// </remarks>
    public bool SeedOnStartup { get; set; }

    /// <summary>
    /// Maximum number of retry attempts for transient database failures. Default: 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts. Default: 5 seconds.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
}
