namespace Granit.DataLookup.Endpoints.Dtos;

/// <summary>Response shape for a single lookup item — wire representation of <c>LookupItem</c>.</summary>
/// <param name="Value">Opaque value (JSON-serialized as given by the source).</param>
/// <param name="Label">Label resolved in the caller's culture.</param>
/// <param name="Extra">Optional extra attributes for secondary rendering.</param>
public sealed record LookupItemResponse(
    object Value,
    string Label,
    IReadOnlyDictionary<string, object?>? Extra);
