namespace Granit.Entities.Visibility;

/// <summary>
/// One conditional-visibility rule, as exposed in the manifest's
/// <c>visibleIf</c> field on a form field, detail section, or relation
/// (per ADR-040, ADR-041, ADR-048).
/// </summary>
/// <remarks>
/// <para>
/// The DSL is deliberately closed (see <see cref="FieldOp"/>) — no eval strings,
/// no JS/Python embedded, no scripted expressions. Sufficient for 95% of admin-CRUD
/// visibility needs; apps requiring more expressive logic should do it server-side
/// via a permission check or a domain rule, not at the manifest layer.
/// </para>
/// <para>
/// <see cref="Value"/> may be any JSON-serializable literal. For <see cref="FieldOp.In"/>
/// and <see cref="FieldOp.NotIn"/>, <see cref="Value"/> must be an enumerable.
/// For <see cref="FieldOp.IsNull"/> and <see cref="FieldOp.IsNotNull"/>, <see cref="Value"/>
/// is ignored.
/// </para>
/// </remarks>
/// <param name="Field">
/// The other field's property name, in the source entity's PascalCase form
/// (e.g. <c>"Status"</c>). Resolved client-side at render time against the
/// current form's value.
/// </param>
/// <param name="Op">The comparison operator.</param>
/// <param name="Value">
/// The literal value (or values for <see cref="FieldOp.In"/> / <see cref="FieldOp.NotIn"/>),
/// or <see langword="null"/> for the unary operators.
/// </param>
public sealed record VisibilityCondition(string Field, FieldOp Op, object? Value);
