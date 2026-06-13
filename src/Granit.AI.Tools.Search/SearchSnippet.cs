namespace Granit.AI.Tools.Search;

/// <summary>
/// A ranked snippet returned by a <c>search</c> tool for the agent to ground its answer on.
/// </summary>
/// <param name="Id">The source record identifier (for citation / follow-up).</param>
/// <param name="Text">The snippet text, or <see langword="null"/> when the backend returned none.</param>
/// <param name="Score">Relevance/similarity score (higher is more relevant), or <see langword="null"/>
/// when the backend does not expose one. Only comparable within a single response.</param>
/// <param name="Source">The retrieval mode that produced the snippet: <c>semantic</c> or <c>full_text</c>.</param>
public sealed record SearchSnippet(string Id, string? Text, double? Score, string Source);
