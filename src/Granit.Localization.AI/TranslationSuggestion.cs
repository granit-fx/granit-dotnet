namespace Granit.Localization.AI;

/// <summary>
/// Represents a single translation suggestion for a specific culture.
/// </summary>
/// <param name="Culture">The target culture code (e.g. <c>"fr"</c>, <c>"en-GB"</c>).</param>
/// <param name="Value">The suggested translated value.</param>
public sealed record TranslationSuggestion(string Culture, string Value);
