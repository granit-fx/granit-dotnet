using System.Text.Json;
using System.Text.Json.Nodes;
using Granit.AI.Tools;

namespace Granit.Imaging.AI.Internal;

/// <summary>
/// Opt-in, default-off <see cref="IAITool"/> that reads text from an image via a Vision-capable
/// workspace (ADR-067). The image bytes are resolved from a model-supplied reference through the
/// application's <see cref="IAIImageSource"/>; the extraction itself routes to a vision workspace,
/// so a text-only chat workspace can still read images. When no vision workspace is configured the
/// tool degrades gracefully with an error result rather than failing the run.
/// </summary>
internal sealed class ExtractTextFromImageTool(
    IAIImageSource imageSource,
    IImageTextExtractor textExtractor) : IAITool, IAIToolInstructions
{
    private static readonly JsonElement Schema = BuildSchema();

    public string Name => "extract_text_from_image";

    public string Description =>
        "Read and return the text contained in an image you cannot otherwise see. "
        + "Pass the reference of the image to read.";

    public JsonElement ParameterSchema => Schema;

    public string Instructions =>
        "Use 'extract_text_from_image' when the user refers to an image and you need the text "
        + "inside it. Pass the image reference from the conversation as 'image'.";

    public async ValueTask<AIToolResult> InvokeAsync(
        AIToolInvocationContext context, CancellationToken cancellationToken = default)
    {
        string? reference = ReadString(context.Arguments, "image");
        if (string.IsNullOrWhiteSpace(reference))
        {
            return AIToolResult.Error("The 'image' argument is required (an image reference).");
        }

        AIImageData? image = await imageSource.GetImageAsync(reference, cancellationToken).ConfigureAwait(false);
        if (image is null)
        {
            return AIToolResult.Error($"Image '{reference}' is not available.");
        }

        ImageTextExtractionResult? extraction = await textExtractor
            .ExtractTextAsync(image.Bytes, image.ContentType, cancellationToken)
            .ConfigureAwait(false);

        if (extraction is null)
        {
            return AIToolResult.Error("No vision-capable workspace is configured, so images cannot be read.");
        }

        var payload = new
        {
            image = reference,
            workspace = extraction.Workspace,
            text = extraction.Text,
        };

        return AIToolResult.Success(JsonSerializer.Serialize(payload, JsonSerializerOptions.Web));
    }

    private static JsonElement BuildSchema()
    {
        JsonObject schema = new()
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["image"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Reference of the image to read (as it appears in the conversation).",
                },
            },
            ["required"] = new JsonArray("image"),
            ["additionalProperties"] = false,
        };

        return JsonSerializer.SerializeToElement(schema);
    }

    private static string? ReadString(JsonElement arguments, string name) =>
        arguments.ValueKind == JsonValueKind.Object
        && arguments.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
