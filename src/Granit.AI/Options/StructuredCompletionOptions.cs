using System.ComponentModel.DataAnnotations;

namespace Granit.AI.Options;

/// <summary>
/// Configuration for <see cref="IStructuredCompletion"/>.
/// </summary>
/// <remarks>
/// Bound to the <c>AI:StructuredCompletion</c> section and validated at startup via
/// <c>ValidateDataAnnotations().ValidateOnStart()</c>.
/// </remarks>
public sealed class StructuredCompletionOptions
{
    /// <summary>Configuration section name in appsettings.json.</summary>
    public const string SectionName = "AI:StructuredCompletion";

    /// <summary>
    /// Maximum time in seconds to wait for a completion to finish. Defaults to <c>30</c>.
    /// </summary>
    [Range(1, 600)]
    public int TimeoutSeconds { get; set; } = 30;
}
