namespace Granit.QueryEngine.Filtering;

/// <summary>
/// Immutable metadata about a date filter with period shortcuts.
/// </summary>
public sealed record DateFilterDescriptor
{
    /// <summary>Property name of the date field (e.g. <c>"CreatedAt"</c>).</summary>
    public required string PropertyName { get; init; }

    /// <summary>CLR type of the date property.</summary>
    public required Type ClrType { get; init; }

    /// <summary>The default date period applied when no explicit period is selected.</summary>
    public DatePeriod DefaultPeriod { get; init; }
}
