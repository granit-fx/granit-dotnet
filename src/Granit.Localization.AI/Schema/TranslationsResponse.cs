namespace Granit.Localization.AI.Schema;

/// <summary>
/// JSON-schema-pinned response shape for the AI translation suggester. The LLM is constrained
/// via <c>ChatResponseFormat.ForJsonSchema&lt;TranslationsResponse&gt;()</c> (ADR-064) so it
/// returns a fixed object rather than a dynamic culture-keyed map — provider-enforced strict
/// schema rejects free-form keys and root-level arrays, hence the wrapping
/// <see cref="Translations"/> array of fixed-shape <see cref="TranslationItem"/> entries.
/// </summary>
public sealed class TranslationsResponse
{
    /// <summary>
    /// One entry per culture the model chose to translate. The service intersects these
    /// against the requested target cultures, so a model that invents a culture code (or
    /// echoes an injected one) contributes nothing to the result.
    /// </summary>
    public List<TranslationItem> Translations { get; set; } = [];
}

/// <summary>
/// A single culture/value pair inside a <see cref="TranslationsResponse"/>.
/// </summary>
public sealed class TranslationItem
{
    /// <summary>Target culture code (e.g. <c>"fr"</c>, <c>"en-GB"</c>).</summary>
    public string Culture { get; set; } = string.Empty;

    /// <summary>Translated value for <see cref="Culture"/>.</summary>
    public string Value { get; set; } = string.Empty;
}
