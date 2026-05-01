using Granit.Entities.Visibility;

namespace Granit.Entities.Forms;

/// <summary>
/// Immutable descriptor for one form field, built via
/// <see cref="FieldBuilder{TEntity, TProperty}"/>.
/// </summary>
public sealed record FieldDescriptor
{
    /// <summary>Property name on the entity (PascalCase, e.g. <c>"Title"</c>).</summary>
    public required string PropertyName { get; init; }

    /// <summary>CLR type of the property.</summary>
    public required Type ClrType { get; init; }

    /// <summary>
    /// Component name from the standard catalog (e.g. <c>"text"</c>, <c>"money"</c>) or
    /// <c>"custom:&lt;app-prefix&gt;-&lt;name&gt;"</c> for app-specific components — see ADR-041.
    /// "Component" replaces "Widget" at the field level; dashboard panels (KPI / Chart /
    /// Table / …) keep the "Widget" naming because they are page-level units.
    /// </summary>
    public required string Component { get; init; }

    /// <summary>
    /// Optional component-specific configuration carried opaquely to the renderer
    /// (e.g. <c>{ currencyCode: "EUR" }</c> for the <c>money</c> component).
    /// </summary>
    public IReadOnlyDictionary<string, object?>? Config { get; init; }

    /// <summary>i18n key for the user-facing label (resolved client-side).</summary>
    public string? LabelKey { get; init; }

    /// <summary>i18n key for the help text shown under the field, if any.</summary>
    public string? HelpKey { get; init; }

    /// <summary>
    /// When set, the field is dropped from the manifest payload entirely if the user
    /// lacks this permission — defense-in-depth per ADR-040 / story #1549. Never just
    /// hidden: a field absent from the payload cannot be inferred client-side.
    /// </summary>
    public string? RequiresPermission { get; init; }

    /// <summary>Display order within the section (lower first).</summary>
    public int Order { get; init; }

    /// <summary>True when the field is read-only in the form context.</summary>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// Optional conditional-visibility rule (per ADR-040). Evaluated client-side
    /// against the current form's values; the field is hidden when the rule is false.
    /// Permission gating (<see cref="RequiresPermission"/>) takes precedence and
    /// happens server-side.
    /// </summary>
    public VisibilityCondition? VisibleIf { get; init; }
}
