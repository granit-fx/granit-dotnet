namespace Granit.Entities.Relations;

/// <summary>
/// Immutable descriptor for one aggregate surfaced by a relation
/// (e.g. <c>Sum(invoice.Amount)</c>). Aggregates are computed by the
/// <c>POST /relations/aggregates</c> batched endpoint (story #1561) and
/// projected back into the relation's payload.
/// </summary>
/// <param name="Kind">The aggregate kind.</param>
/// <param name="PropertyName">PascalCase property name on the related entity, or <see langword="null"/> for <see cref="RelationAggregateKind.Count"/>.</param>
/// <param name="LabelKey">i18n key for the user-facing label (e.g. <c>"Relation:Party.Invoices.Total"</c>).</param>
/// <param name="Format">Optional formatter hint (e.g. <c>"currency"</c>, <c>"int"</c>) — opaque to the framework, mirrored from <see cref="Forms.FieldDescriptor.Component"/> conventions.</param>
public sealed record RelationAggregateDescriptor(
    RelationAggregateKind Kind,
    string? PropertyName,
    string? LabelKey,
    string? Format);
