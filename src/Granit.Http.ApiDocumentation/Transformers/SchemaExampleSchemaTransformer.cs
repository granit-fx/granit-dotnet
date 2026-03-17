using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Granit.Http.ApiDocumentation.Transformers;

/// <summary>
/// Applies JSON examples to OpenAPI schemas by collecting examples from all
/// registered <see cref="ISchemaExampleProvider"/> instances via DI.
/// Each provider supplies examples for its own module's Request types,
/// keeping DTOs free of OpenAPI coupling.
/// </summary>
internal sealed class SchemaExampleSchemaTransformer(
    IEnumerable<ISchemaExampleProvider> providers) : IOpenApiSchemaTransformer
{
    private readonly Dictionary<Type, JsonNode> _examples = BuildExampleMap(providers);

    /// <inheritdoc/>
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken)
    {
        if (_examples.TryGetValue(context.JsonTypeInfo.Type, out JsonNode? example))
        {
            schema.Example = example.DeepClone();
        }

        return Task.CompletedTask;
    }

    private static Dictionary<Type, JsonNode> BuildExampleMap(
        IEnumerable<ISchemaExampleProvider> providers)
    {
        Dictionary<Type, JsonNode> map = [];

        foreach (ISchemaExampleProvider provider in providers)
        {
            foreach (KeyValuePair<Type, JsonNode> entry in provider.GetExamples())
            {
                map.TryAdd(entry.Key, entry.Value);
            }
        }

        return map;
    }
}
