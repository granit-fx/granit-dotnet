using System.Text.Json.Serialization;

namespace Granit.Entities.Customization.Domain.Deltas;

/// <summary>
/// Closed base for the Layer 1 delta vocabulary (ADR-053): the framework only
/// recognises <see cref="ReorderDelta"/>, <see cref="RegroupDelta"/>, and
/// <see cref="HideDelta"/>. Any other shape is rejected at the endpoint
/// boundary — extending the vocabulary requires an ADR amendment.
/// </summary>
/// <remarks>
/// The polymorphism attributes drive <see cref="System.Text.Json"/>
/// serialization (used by the EF Core companion to round-trip the deltas
/// through the JSON column and by the endpoint layer for the wire shape).
/// Discriminator values are stable wire identifiers — never rename them
/// without a database migration.
/// </remarks>
/// <param name="FieldName">The compiled field this delta targets. MUST resolve in the underlying <c>EntityDefinitionDescriptor</c>.</param>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ReorderDelta), "reorder")]
[JsonDerivedType(typeof(RegroupDelta), "regroup")]
[JsonDerivedType(typeof(HideDelta), "hide")]
public abstract record LayoutDelta(string FieldName);
