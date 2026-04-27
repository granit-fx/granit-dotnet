namespace Granit.Mergeable;

/// <summary>
/// Per-field admin overrides for a merge. Each entry says "for field <c>X</c> use the
/// survivor's value or the loser's value". Missing keys fall back to the recommended
/// default returned by <c>IMergeable.GetConflicts</c>.
/// </summary>
/// <remarks>
/// <para>
/// Field paths are dot-separated and can target nested values inside dictionaries —
/// e.g. <c>"Name"</c>, <c>"TaxStatus"</c>, <c>"Metadata.segment"</c>,
/// <c>"ExternalMappings.stripe"</c>.
/// </para>
/// <para>
/// Use <see cref="Empty"/> to defer entirely to defaults, or <see cref="Builder"/> for
/// fluent construction.
/// </para>
/// </remarks>
public sealed class MergeFieldChoices
{
    /// <summary>Empty choices — every field defaults to the recommended <see cref="WinnerSide"/>.</summary>
    public static MergeFieldChoices Empty { get; } = new(new Dictionary<string, WinnerSide>(StringComparer.Ordinal));

    /// <summary>Map of field path → winner side.</summary>
    public IReadOnlyDictionary<string, WinnerSide> Choices { get; }

    /// <summary>Creates a frozen choices set from a dictionary. Use <see cref="Builder"/> for fluent style.</summary>
    public MergeFieldChoices(IReadOnlyDictionary<string, WinnerSide> choices)
    {
        ArgumentNullException.ThrowIfNull(choices);
        Choices = choices;
    }

    /// <summary>
    /// Resolves the winner for <paramref name="fieldPath"/>. Returns the explicit override
    /// if present; otherwise <paramref name="defaultSide"/>.
    /// </summary>
    public WinnerSide ResolveOrDefault(string fieldPath, WinnerSide defaultSide) =>
        Choices.TryGetValue(fieldPath, out WinnerSide side) ? side : defaultSide;

    /// <summary>Fluent builder for <see cref="MergeFieldChoices"/>.</summary>
    public static MergeFieldChoicesBuilder NewBuilder() => new();
}

/// <summary>Fluent builder for <see cref="MergeFieldChoices"/>.</summary>
public sealed class MergeFieldChoicesBuilder
{
    private readonly Dictionary<string, WinnerSide> _choices = new(StringComparer.Ordinal);

    /// <summary>Sets the winner for <paramref name="fieldPath"/>.</summary>
    public MergeFieldChoicesBuilder With(string fieldPath, WinnerSide side)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldPath);
        _choices[fieldPath] = side;
        return this;
    }

    /// <summary>Builds the immutable <see cref="MergeFieldChoices"/>.</summary>
    public MergeFieldChoices Build() => new(_choices);
}
