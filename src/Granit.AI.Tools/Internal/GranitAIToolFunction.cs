using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Granit.AI.Tools.Internal;

/// <summary>
/// Adapts an <see cref="IAITool"/> to a <see cref="Microsoft.Extensions.AI"/>
/// <see cref="AIFunction"/> so it can be placed on <c>ChatOptions.Tools</c> and invoked by
/// the provider pipeline. Declaration (<see cref="Name"/>, <see cref="Description"/>,
/// <see cref="JsonSchema"/>) and invocation both forward to the wrapped tool.
/// </summary>
internal sealed class GranitAIToolFunction(IAITool tool) : AIFunction
{
    private static readonly JsonSerializerOptions ArgumentSerializerOptions =
        new(JsonSerializerDefaults.Web);

    public override string Name => tool.Name;

    public override string Description => tool.Description;

    public override JsonElement JsonSchema => tool.ParameterSchema;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        // AIFunctionArguments carries pipeline state (Services, Context) beyond the raw
        // key/value pairs; serialize only the entries the tool's schema describes.
        Dictionary<string, object?> values = new(arguments.Count, StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> argument in arguments)
        {
            values[argument.Key] = argument.Value;
        }

        JsonElement payload = JsonSerializer.SerializeToElement(values, ArgumentSerializerOptions);

        AIToolResult result = await tool
            .InvokeAsync(new AIToolInvocationContext { Arguments = payload }, cancellationToken)
            .ConfigureAwait(false);

        return result.Content;
    }
}
