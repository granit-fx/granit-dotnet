namespace Granit.Entities.Forms;

/// <summary>
/// One choice for a <c>select</c> / <c>multiselect</c> / <c>status</c> field component
/// (ADR-041). Emitted in the field's opaque <c>config["options"]</c> array and consumed
/// verbatim by the React renderer.
/// </summary>
/// <param name="Value">
/// Wire value sent back to the server. For enum-backed fields this is the member's
/// PascalCase name (symmetric with <c>JsonStringEnumConverter</c>).
/// </param>
/// <param name="LabelKey">
/// i18n key for the human label (resolved client-side), or <see langword="null"/> to fall
/// back to the raw <see cref="Value"/>. Enum-backed fields use the <c>Enum:{TypeName}.{Value}</c>
/// convention (ADR-023), shared with <c>EnumLookupSource</c>.
/// </param>
public sealed record FieldSelectOption(object? Value, string? LabelKey);
