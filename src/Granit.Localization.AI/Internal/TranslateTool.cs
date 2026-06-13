using System.Text.Json;
using System.Text.Json.Nodes;
using Granit.AI.Permissions;
using Granit.AI.Tools;

namespace Granit.Localization.AI.Internal;

/// <summary>
/// A gated capability tool (ADR-067, "agent-as-tool" single-shot sub-agent) that wraps
/// <see cref="ITranslationSuggestionService"/> as the <c>translate</c> chat tool. Gated by
/// <see cref="AIPermissions.ChatTools.Translate"/> so admins enable it per user/role.
/// </summary>
/// <remarks>
/// This is the reference pattern for wrapping an existing <c>*.AI</c> capability as a chat tool:
/// implement <see cref="IAITool"/> (declaration + invocation that delegates to the capability
/// service), add <see cref="IGatedAITool"/> with the capability's permission, and register it
/// through an opt-in extension on <c>AIToolRegistrationBuilder</c>.
/// </remarks>
internal sealed class TranslateTool(ITranslationSuggestionService translationService)
    : IAITool, IGatedAITool, IAIToolInstructions
{
    private static readonly JsonElement Schema = BuildSchema();

    public string Name => "translate";

    public string Description =>
        "Translate a short piece of text into a target language. Returns the translated text.";

    public string RequiredPermission => AIPermissions.ChatTools.Translate;

    public JsonElement ParameterSchema => Schema;

    public string Instructions =>
        "Use 'translate' to render text in another language. Provide the target language as a "
        + "culture code (e.g. 'fr', 'de', 'pt-BR'); pass 'source_language' when you know it.";

    public async ValueTask<AIToolResult> InvokeAsync(
        AIToolInvocationContext context, CancellationToken cancellationToken = default)
    {
        string? text = ReadString(context.Arguments, "text");
        string? target = ReadString(context.Arguments, "target_language");

        if (string.IsNullOrWhiteSpace(text))
        {
            return AIToolResult.Error("The 'text' argument is required.");
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            return AIToolResult.Error("The 'target_language' argument is required (a culture code).");
        }

        string source = ReadString(context.Arguments, "source_language") ?? "en";

        IReadOnlyList<TranslationSuggestion> suggestions = await translationService
            .SuggestTranslationsAsync(
                key: "chat.translate",
                sourceValue: text,
                sourceCulture: source,
                targetCultures: [target],
                context: TranslationContext.Description,
                cancellationToken)
            .ConfigureAwait(false);

        TranslationSuggestion? suggestion = suggestions.Count > 0 ? suggestions[0] : null;
        if (suggestion is null)
        {
            return AIToolResult.Error("Translation is unavailable right now.");
        }

        var payload = new
        {
            source_language = source,
            target_language = suggestion.Culture,
            translation = suggestion.Value,
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
                ["text"] = new JsonObject { ["type"] = "string", ["description"] = "The text to translate." },
                ["target_language"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Target culture code, e.g. 'fr', 'de', 'pt-BR'.",
                },
                ["source_language"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Source culture code; defaults to 'en' when omitted.",
                },
            },
            ["required"] = new JsonArray("text", "target_language"),
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
