using System.Text.Json;
using System.Text.Json.Serialization;
using Granit.Http.Idempotency.Models;

namespace Granit.Http.Idempotency.Internal;

[JsonSerializable(typeof(IdempotencyEntry))]
[JsonSerializable(typeof(Dictionary<string, string[]>))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class IdempotencyJsonContext : JsonSerializerContext
{
}
