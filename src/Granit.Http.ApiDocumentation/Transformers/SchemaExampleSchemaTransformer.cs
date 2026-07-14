using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace Granit.Http.ApiDocumentation.Transformers;

/// <summary>
/// Applies JSON examples to OpenAPI schemas by collecting examples from all
/// registered <see cref="ISchemaExampleProvider"/> instances via DI.
/// Each provider supplies examples for its own module's Request types,
/// keeping DTOs free of OpenAPI coupling.
/// </summary>
internal sealed partial class SchemaExampleSchemaTransformer : IOpenApiSchemaTransformer
{
    private readonly Dictionary<Type, JsonNode> _examples;

    public SchemaExampleSchemaTransformer(
        IEnumerable<ISchemaExampleProvider> providers,
        ILogger<SchemaExampleSchemaTransformer> logger)
    {
        _examples = BuildExampleMap(providers);

        // Provider discovery is assembly-scan based (module registry, or AppDomain
        // fallback) — an empty result usually means the scan ran before the provider
        // assemblies were loaded. Surface it instead of silently emitting example-less
        // documents.
        if (_examples.Count == 0)
        {
            LogNoSchemaExampleProviders(logger);
        }
    }

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

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "No ISchemaExampleProvider implementations discovered — OpenAPI schemas " +
                  "are generated without examples. If your modules ship providers, ensure " +
                  "their assemblies are part of the Granit module graph.")]
    private static partial void LogNoSchemaExampleProviders(ILogger logger);
}
