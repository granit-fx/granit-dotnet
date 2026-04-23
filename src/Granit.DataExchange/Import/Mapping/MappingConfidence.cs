namespace Granit.DataExchange.Import.Mapping;

/// <summary>
/// Confidence level of a column mapping suggestion.
/// Lower values indicate higher confidence (manual overrides everything).
/// </summary>
public enum MappingConfidence
{
    /// <summary>User-confirmed manual mapping (highest priority).</summary>
    Manual = 0,

    /// <summary>Mapping previously saved and reused.</summary>
    Saved = 1,

    /// <summary>Exact case-insensitive match on property name or display name.</summary>
    Exact = 2,

    /// <summary>Fuzzy match (Levenshtein distance within threshold).</summary>
    Fuzzy = 3,

    /// <summary>AI-assisted semantic match (header metadata only, GDPR-safe).</summary>
    Semantic = 4,
}
