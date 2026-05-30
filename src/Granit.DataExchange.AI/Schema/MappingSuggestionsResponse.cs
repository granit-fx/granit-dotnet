namespace Granit.DataExchange.AI.Schema;

/// <summary>
/// JSON-schema-pinned response shape for the AI semantic mapper. The LLM is constrained via
/// <c>ChatResponseFormat.ForJsonSchema&lt;MappingSuggestionsResponse&gt;()</c> (ADR-064). The
/// suggestions are wrapped in this fixed object rather than returned as a root-level array,
/// because provider-enforced strict schema rejects a top-level array.
/// </summary>
public sealed class MappingSuggestionsResponse
{
    /// <summary>
    /// Proposed column-to-property mappings. The service validates each entry against the
    /// known source columns and target properties and drops anything outside that universe,
    /// so a model that invents a column or property contributes nothing.
    /// </summary>
    public List<MappingSuggestionItem> Mappings { get; set; } = [];
}

/// <summary>
/// A single source-column → target-property mapping inside a <see cref="MappingSuggestionsResponse"/>.
/// </summary>
public sealed class MappingSuggestionItem
{
    /// <summary>Exact source column name from the imported file.</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Exact target property path on the destination entity.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>Model confidence in the mapping, clamped to <c>0.0..1.0</c> by the service.</summary>
    public double Score { get; set; }
}
