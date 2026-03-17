using System.Text.Json.Nodes;

namespace Granit.Http.ApiDocumentation;

/// <summary>
/// Provides OpenAPI schema examples for specific CLR types.
/// Implement this interface in each <c>*.Endpoints</c> package to supply
/// realistic example values for Request DTOs without coupling them to OpenAPI.
/// </summary>
public interface ISchemaExampleProvider
{
    /// <summary>
    /// Returns a mapping from CLR type to its JSON example.
    /// Each <see cref="JsonNode"/> will be deep-cloned before being assigned to the schema.
    /// </summary>
    IReadOnlyDictionary<Type, JsonNode> GetExamples();
}
