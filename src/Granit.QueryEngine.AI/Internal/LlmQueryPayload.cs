namespace Granit.QueryEngine.AI.Internal;

/// <summary>
/// Schema-pinned DTO (ADR-064) for the structured-completion response. Maps to
/// <see cref="Granit.QueryEngine.QueryRequest"/> after metadata validation.
/// </summary>
/// <remarks>
/// <see cref="Filter"/> is a list of fixed-shape <see cref="LlmFilterClause"/> entries rather than
/// a dynamic-keyed map: provider-enforced strict JSON schema rejects free-form object keys, so the
/// <c>"field.operator"</c> key travels inside each clause and is reassembled into a dictionary by
/// the translator.
/// </remarks>
internal sealed class LlmQueryPayload
{
    public int? Page { get; set; }

    public int? PageSize { get; set; }

    public string? Sort { get; set; }

    public List<LlmFilterClause>? Filter { get; set; }

    public List<string>? QuickFilters { get; set; }

    public string? GroupBy { get; set; }
}

/// <summary>
/// A single filter clause: a <c>"fieldName.operator"</c> key and its string value.
/// </summary>
internal sealed class LlmFilterClause
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
