namespace Granit.Entities.Customization.Domain.Deltas;

/// <summary>
/// Closed base for the Layer 1 delta vocabulary (ADR-053): the framework only
/// recognises <see cref="ReorderDelta"/>, <see cref="RegroupDelta"/>, and
/// <see cref="HideDelta"/>. Any other shape is rejected at the endpoint
/// boundary — extending the vocabulary requires an ADR amendment.
/// </summary>
/// <param name="FieldName">The compiled field this delta targets. MUST resolve in the underlying <c>EntityDefinitionDescriptor</c>.</param>
public abstract record LayoutDelta(string FieldName);
