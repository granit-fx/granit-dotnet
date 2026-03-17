using System.Text.Json.Nodes;
using Granit.Http.ApiDocumentation;
using Granit.Timeline;

namespace Granit.Timeline.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for timeline Request DTOs.
/// </summary>
internal sealed class TimelineSchemaExampleProvider : ISchemaExampleProvider
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            [typeof(PostTimelineEntryRequest)] = new JsonObject
            {
                ["entryType"] = "Comment",
                ["body"] = "Patient file reviewed and approved by Dr. Martin.",
                ["parentEntryId"] = (JsonNode?)null,
                ["attachmentBlobIds"] = new JsonArray
                {
                    "c1d2e3f4-a5b6-7890-cdef-1234567890ab",
                },
            },
        };
}
