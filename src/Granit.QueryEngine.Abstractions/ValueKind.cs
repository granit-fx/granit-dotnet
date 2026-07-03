namespace Granit.QueryEngine;

/// <summary>
/// Semantic kind of a column (or metric) value — a display-type hint that tells the frontend
/// <em>what a value means</em> so it can pick the right cell renderer (and, on the write side,
/// the right edit input) without inspecting the CLR type. Orthogonal to the raw
/// <see cref="ColumnBuilder{TEntity}.Format(string)"/> hint, which only refines <em>how</em> a
/// value is rendered.
/// </summary>
/// <remarks>
/// Carried by name on the wire (<c>JsonStringEnumConverter</c>) — metadata only, never persisted,
/// so the enum-as-string persistence convention does not apply. A <c>null</c> value-kind means
/// "fall back to the CLR type", preserving the pre-existing behaviour. Only <see cref="Currency"/>
/// needs a companion parameter (the sibling <see cref="ColumnDescriptor.CurrencyCode"/>); the enum
/// itself carries no embedded parameter.
/// </remarks>
public enum ValueKind
{
    // --- Tier 1: shared with the Analytics metric vocabulary (names are wire-frozen) ---

    /// <summary>Plain count of items, rendered as an integer.</summary>
    Count,

    /// <summary>Generic numeric value — locale separators, optional decimals.</summary>
    Number,

    /// <summary>
    /// Monetary amount. Pair with <see cref="ColumnDescriptor.CurrencyCode"/> (ISO 4217) so the
    /// frontend formats with the right symbol and locale.
    /// </summary>
    Currency,

    /// <summary>Ratio in <c>[0, 1]</c>, rendered as a percentage.</summary>
    Percentage,

    /// <summary>Duration in seconds, rendered with the locale's duration formatting.</summary>
    Duration,

    /// <summary>Date (no time component), rendered with the locale's date formatting.</summary>
    Date,

    // --- Tier 2: high-value display types ---

    /// <summary>Absolute or relative URL, rendered as a clickable link.</summary>
    Url,

    /// <summary>Email address, rendered as a <c>mailto:</c> link.</summary>
    Email,

    /// <summary>Telephone number, rendered as a <c>tel:</c> link.</summary>
    Phone,

    /// <summary>Data size in bytes, rendered humanized (KB / MB / GB).</summary>
    Bytes,

    /// <summary>Date and time instant, rendered with the locale's date+time formatting.</summary>
    DateTime,

    /// <summary>Boolean flag, rendered as a checkmark or yes/no badge.</summary>
    Boolean,

    /// <summary>Enumeration value, rendered as a (localized) badge or chip.</summary>
    Enum,

    // --- Tier 3: richer renderers ---

    /// <summary>Instant rendered relative to now (e.g. "3 hours ago").</summary>
    RelativeTime,

    /// <summary>Time of day (no date component).</summary>
    Time,

    /// <summary>Color value (e.g. hex), rendered as a swatch.</summary>
    Color,

    /// <summary>Image URL, rendered as a thumbnail.</summary>
    Image,

    /// <summary>Structured JSON payload, rendered in a code block.</summary>
    Json,

    /// <summary>Markdown source, rendered as formatted text.</summary>
    Markdown,

    /// <summary>Collection of labels, rendered as a list of chips.</summary>
    Tags,

    /// <summary>Numeric score, rendered as stars.</summary>
    Rating,

    /// <summary>Opaque identifier (e.g. GUID / key), rendered in monospace.</summary>
    Identifier,
}
