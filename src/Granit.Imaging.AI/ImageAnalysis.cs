namespace Granit.Imaging.AI;

/// <summary>
/// Result of an AI-powered image analysis.
/// </summary>
/// <param name="Description">A natural-language description of the image content.</param>
/// <param name="DetectedObjects">Objects identified in the image (e.g. "car", "person", "building").</param>
/// <param name="Tags">Semantic tags for classification and search (e.g. "outdoor", "urban", "daytime").</param>
/// <param name="SuggestedAltText">Suggested alt text for accessibility (WCAG 2.1). May be <c>null</c> if the model cannot generate one.</param>
public sealed record ImageAnalysis(
    string Description,
    IReadOnlyList<string> DetectedObjects,
    IReadOnlyList<string> Tags,
    string? SuggestedAltText);
