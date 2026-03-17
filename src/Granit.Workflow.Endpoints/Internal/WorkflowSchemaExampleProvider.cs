using System.Text.Json.Nodes;
using Granit.Http.ApiDocumentation;
using Granit.Workflow.Endpoints.Dtos;

namespace Granit.Workflow.Endpoints.Internal;

/// <summary>
/// Provides OpenAPI schema examples for workflow Request DTOs.
/// </summary>
internal sealed class WorkflowSchemaExampleProvider : ISchemaExampleProvider
{
    /// <inheritdoc/>
    public IReadOnlyDictionary<Type, JsonNode> GetExamples() =>
        new Dictionary<Type, JsonNode>
        {
            [typeof(WorkflowTransitionRequest)] = new JsonObject
            {
                ["targetState"] = "Published",
                ["comment"] = "Approved by medical director.",
            },
        };
}
