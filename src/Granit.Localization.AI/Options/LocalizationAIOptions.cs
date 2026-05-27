using System.ComponentModel.DataAnnotations;

namespace Granit.Localization.AI.Options;

/// <summary>
/// Configuration options for AI-powered translation suggestions.
/// </summary>
/// <remarks>
/// Bound to the <c>AI:Localization</c> configuration section and validated at startup via
/// <c>ValidateDataAnnotations().ValidateOnStart()</c> so an out-of-range value aborts
/// host boot instead of silently degrading every suggestion call.
/// </remarks>
public sealed class LocalizationAIOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "AI:Localization";

    /// <summary>
    /// AI workspace name to use for translation. Defaults to <c>"default"</c>.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string WorkspaceName { get; set; } = "default";

    /// <summary>
    /// Maximum time in seconds to wait for translations to complete.
    /// Defaults to <c>30</c> (17 cultures can be slow).
    /// </summary>
    [Range(1, 600)]
    public int TimeoutSeconds { get; set; } = 30;
}
