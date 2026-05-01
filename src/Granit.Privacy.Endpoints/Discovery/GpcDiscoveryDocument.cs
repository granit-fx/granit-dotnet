using System.Text.Json.Serialization;

namespace Granit.Privacy.Endpoints.Discovery;

/// <summary>
/// Wire shape of the GPC discovery document served at <c>/.well-known/gpc.json</c>
/// per <see href="https://w3c.github.io/gpc/#the-well-known-resource"/>.
/// </summary>
/// <param name="Gpc">Always <see langword="true"/> when the document is published — the operator declares support for GPC.</param>
/// <param name="LastUpdate">RFC 3339 full-date marking when the operator made the statement of support. Serialized as <c>YYYY-MM-DD</c>.</param>
internal sealed record GpcDiscoveryDocument(
    [property: JsonPropertyName("gpc")] bool Gpc,
    [property: JsonPropertyName("lastUpdate")] DateOnly LastUpdate);
