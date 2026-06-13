namespace Granit.AI.Tools.Search.Options;

/// <summary>
/// Bounds for the <c>search</c> tools.
/// </summary>
public sealed class GranitAIToolsSearchOptions
{
    /// <summary>Configuration section path.</summary>
    public const string SectionName = "AI:Tools:Search";

    /// <summary>Number of snippets returned when the model does not specify a limit. Default 5.</summary>
    public int DefaultLimit { get; set; } = 5;

    /// <summary>Maximum number of snippets a single call may return. Default 20.</summary>
    public int MaxLimit { get; set; } = 20;
}
