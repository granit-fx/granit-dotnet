namespace Granit.Wolverine.Options;

/// <summary>
/// Configuration options for Granit Wolverine core (provider-agnostic).
/// </summary>
/// <remarks>
/// Bound from the <c>"Wolverine"</c> section of <c>appsettings.json</c>.
/// Does not contain any transport connection string — those live in provider-specific options
/// (e.g., <c>WolverinePostgresqlOptions</c>).
/// </remarks>
public sealed class WolverineMessagingOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Wolverine";

    /// <summary>
    /// Cooldown delays between successive retry attempts.
    /// The number of elements determines the retry count per exception.
    /// Default: 5 s / 30 s / 5 min.
    /// </summary>
    public TimeSpan[] RetryDelays { get; set; } =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(5),
    ];

    /// <summary>
    /// Maximum number of retry attempts applied to <see cref="RetryDelays"/>.
    /// Must be ≥ 1. Default: 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
}
