using JasperFx.CodeGeneration;

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

    /// <summary>
    /// Wolverine handler/endpoint code-generation mode. Default <see cref="TypeLoadMode.Dynamic"/>:
    /// types are generated at runtime via Roslyn (requires <c>WolverineFx.RuntimeCompilation</c>,
    /// referenced transitively by <c>Granit.Wolverine</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Set to <see cref="TypeLoadMode.Static"/> in production to remove cold-start Roslyn
    /// compilation and its memory overhead. <b>Static requires pre-generated code</b>: run
    /// <c>dotnet run -- codegen write</c> in the build pipeline (e.g. a Docker build stage) and
    /// ship the generated <c>Internal/Generated/</c> output — otherwise the host throws at startup
    /// (no runtime fallback, no environment-based switching).
    /// </para>
    /// <para>
    /// Bind via <c>"Wolverine:CodeGenerationMode": "Static"</c>. <see cref="TypeLoadMode.Auto"/>
    /// is accepted but discouraged by the Wolverine community.
    /// </para>
    /// </remarks>
    public TypeLoadMode CodeGenerationMode { get; set; } = TypeLoadMode.Dynamic;
}
