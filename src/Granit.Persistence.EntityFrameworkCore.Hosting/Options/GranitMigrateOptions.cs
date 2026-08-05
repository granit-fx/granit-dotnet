using System.ComponentModel.DataAnnotations;

namespace Granit.Persistence.EntityFrameworkCore.Hosting.Options;

/// <summary>
/// Configuration options for the Granit migration runner. Bound from the
/// <see cref="SectionName"/> configuration section by <c>AddGranitMigrateSupport()</c>;
/// a code-level configure delegate passed to that method wins over configuration values.
/// </summary>
public sealed class GranitMigrateOptions
{
    /// <summary>
    /// Configuration section name in <c>appsettings.json</c>: <c>"Persistence:Migrate"</c>.
    /// </summary>
    /// <remarks>
    /// Distinct from <c>"Persistence:Migrations"</c> (<c>MigrationStartupOptions</c>), which
    /// configures data-migration batch execution — this section configures the runner itself.
    /// </remarks>
    public const string SectionName = "Persistence:Migrate";

    /// <summary>
    /// The CLI argument that triggers migration mode. Default: <c>"--migrate"</c>.
    /// </summary>
    /// <remarks>
    /// Only CLI arguments are supported — environment variables are intentionally excluded
    /// to prevent Kubernetes CrashLoopBackOff when accidentally set on a Deployment.
    /// </remarks>
    [Required]
    public string CliFlag { get; set; } = "--migrate";

    /// <summary>
    /// Name of the connection string used by the migration runner's schema-ensuring passes
    /// and by the provider distributed locks. Default: <c>"DefaultConnection"</c>.
    /// </summary>
#pragma warning disable GRSEC003 // Property holds a connection string NAME (configuration key), not a secret
    [Required]
    public string ConnectionStringName { get; set; } = "DefaultConnection";
#pragma warning restore GRSEC003

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
    /// When <c>true</c>, <see cref="Granit.Persistence.EntityFrameworkCore.DataSeeding.DataSeedingHostedService"/>
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
    [Range(1, 100)]
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts. Default: 5 seconds.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(5);
}
