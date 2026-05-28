namespace Granit.DataExchange.Import;

/// <summary>
/// Fluent builder for configuring how a single property is imported.
/// </summary>
public sealed class PropertyMappingBuilder
{
    internal string? DisplayNameValue { get; private set; }
    internal string? DescriptionValue { get; private set; }
    internal List<string> AliasValues { get; } = [];
    internal bool IsRequired { get; private set; }
    internal string? FormatValue { get; private set; }

    /// <summary>
    /// Sets the user-facing display name for this property.
    /// Used in preview UI and for mapping suggestions (exact/fuzzy/AI).
    /// </summary>
    public PropertyMappingBuilder DisplayName(string name)
    {
        DisplayNameValue = name;
        return this;
    }

    /// <summary>
    /// Sets a description for this property.
    /// Sent to the AI mapping service as <see cref="ImportFieldMetadata.Description"/>.
    /// </summary>
    public PropertyMappingBuilder Description(string description)
    {
        DescriptionValue = description;
        return this;
    }

    /// <summary>
    /// Adds alternative names that should match this property.
    /// Used for exact and fuzzy matching (e.g. <c>"Courriel"</c>, <c>"Mail"</c> → <c>Email</c>).
    /// </summary>
    public PropertyMappingBuilder Aliases(params ReadOnlySpan<string> aliases)
    {
        AliasValues.AddRange(aliases);
        return this;
    }

    /// <summary>
    /// Marks this property as required for import.
    /// Independent of the entity's <c>[Required]</c> attribute — this controls import validation only.
    /// </summary>
    public PropertyMappingBuilder Required(bool required = true)
    {
        IsRequired = required;
        return this;
    }

    /// <summary>
    /// Sets the expected format for type conversion (e.g. <c>"dd/MM/yyyy"</c> for dates).
    /// </summary>
    public PropertyMappingBuilder Format(string format)
    {
        FormatValue = format;
        return this;
    }
}
