using Granit.Entities.Relations;

namespace Granit.Entities.Endpoints.Dtos;

/// <summary>
/// Request body for <c>POST /api/entities/{name}/{id}/relations/aggregates</c>.
/// </summary>
/// <param name="Relations">
/// Names of the relations whose aggregates the caller wants. When
/// <see langword="null"/> or empty, the endpoint resolves "every relation
/// the caller can read on the source entity" — convenient for a detail page
/// that wants every smart-button count in one round-trip.
/// </param>
public sealed record RelationAggregatesRequest(IReadOnlyList<string>? Relations);

/// <summary>
/// Response body — keyed by relation name, each entry carries the computed
/// aggregate values. Relations the caller cannot read are absent (defense
/// in depth, story #1562 acceptance).
/// </summary>
/// <param name="Aggregates">One entry per resolved relation.</param>
public sealed record RelationAggregatesResponse(
    IReadOnlyDictionary<string, RelationAggregateValue> Aggregates);
