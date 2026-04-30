namespace Granit.Entities.Relations;

/// <summary>
/// Computed aggregate values for one relation, returned by
/// <see cref="IRelationAggregateService"/>. Each numeric slot is nullable —
/// the runner only fills the aggregates the relation declared (a relation
/// without <see cref="RelationAggregateKind.Sum"/> in its descriptor leaves
/// <see cref="Sum"/> null).
/// </summary>
/// <param name="Count">Count of related rows. <see langword="null"/> when the relation does not declare a Count aggregate.</param>
/// <param name="Sum">Sum over a property. <see langword="null"/> when no Sum was declared, or when the relation contains zero rows (per ADR-038 empty-set semantics).</param>
/// <param name="Avg">Average over a property. <see langword="null"/> on empty sets.</param>
/// <param name="Min">Min over a property. <see langword="null"/> on empty sets.</param>
/// <param name="Max">Max over a property. <see langword="null"/> on empty sets.</param>
/// <param name="Currency">Optional ISO 4217 currency code when the aggregate's <see cref="RelationAggregateDescriptor.Format"/> is <c>"currency"</c>.</param>
public sealed record RelationAggregateValue(
    long? Count,
    decimal? Sum,
    decimal? Avg,
    decimal? Min,
    decimal? Max,
    string? Currency = null);
