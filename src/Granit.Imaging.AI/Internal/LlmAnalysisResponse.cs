namespace Granit.Imaging.AI.Internal;

/// <summary>
/// Wire shape of the LLM image-analysis response, produced and deserialized by the
/// structured-output primitive (camelCase, case-insensitive). Sanitized into the public
/// <see cref="ImageAnalysis"/> by <see cref="LlmImageAnalyzer"/>.
/// </summary>
internal sealed record LlmAnalysisResponse(
    string? Description,
    IReadOnlyList<string>? DetectedObjects,
    IReadOnlyList<string>? Tags,
    string? SuggestedAltText);
