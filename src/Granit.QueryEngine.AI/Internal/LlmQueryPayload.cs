namespace Granit.QueryEngine.AI.Internal;

/// <summary>
/// Internal DTO for JSON deserialization of the LLM response.
/// Maps to <see cref="QueryRequest"/> after validation.
/// </summary>
internal sealed class LlmQueryPayload
{
    public int? Page { get; set; }

    public int? PageSize { get; set; }

    public string? Sort { get; set; }

    public Dictionary<string, string>? Filter { get; set; }

    public List<string>? QuickFilters { get; set; }

    public string? GroupBy { get; set; }
}
