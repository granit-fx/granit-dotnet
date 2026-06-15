using System.Text.Json;
using System.Text.Json.Nodes;
using Granit.AI.Chat.Clarification;
using Granit.AI.Tools;

namespace Granit.AI.Chat.Internal;

/// <summary>
/// The <c>request_clarification</c> chat tool (ADR-067). When the agent is ambiguous about the
/// user's intent it calls this tool with a question and discrete options; the tool halts the
/// agentic loop and surfaces a typed <see cref="AIClarificationRequest"/> to the caller instead of
/// answering. The front renders the options as buttons; the user's choice becomes the next turn.
/// Always available to chat agents (registered by <c>GranitAIChatModule</c>).
/// </summary>
internal sealed class RequestClarificationTool : IAITool, IAIToolInstructions
{
    private const string DescriptionKey = "description";

    private static readonly JsonElement Schema = BuildSchema();

    private static readonly JsonSerializerOptions PayloadOptions = JsonSerializerOptions.Web;

    public string Name => "request_clarification";

    public string Description =>
        "Ask the user a clarifying question with discrete clickable options when their intent is "
        + "ambiguous. Halts until the user answers — do not also answer in prose.";

    public string Instructions =>
        "When the request is ambiguous, call 'request_clarification' with a concise 'question' and "
        + "2-5 'options' (each a short 'label'), instead of guessing. Set 'allow_other' when a "
        + "free-text answer should also be offered. Do not call it for trivial or rhetorical asks.";

    public JsonElement ParameterSchema => Schema;

    public ValueTask<AIToolResult> InvokeAsync(
        AIToolInvocationContext context, CancellationToken cancellationToken = default)
    {
        string? question = ReadString(context.Arguments, "question");
        if (string.IsNullOrWhiteSpace(question))
        {
            return ValueTask.FromResult(AIToolResult.Error("The 'question' argument is required."));
        }

        List<AIClarificationOption> options = ReadOptions(context.Arguments);
        if (options.Count == 0)
        {
            return ValueTask.FromResult(AIToolResult.Error("At least one 'options' entry with a 'label' is required."));
        }

        var clarification = new AIClarificationRequest
        {
            Question = question,
            Options = options,
            AllowOther = ReadBool(context.Arguments, "allow_other"),
        };

        string payload = JsonSerializer.Serialize(clarification, PayloadOptions);
        return ValueTask.FromResult(AIToolResult.Halt(
            AIClarificationRequest.InterruptKind, payload, "Clarification requested; awaiting the user's choice."));
    }

    private static List<AIClarificationOption> ReadOptions(JsonElement arguments)
    {
        List<AIClarificationOption> options = [];
        if (arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty("options", out JsonElement array)
            && array.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in array.EnumerateArray())
            {
                string? label = ReadString(item, "label");
                if (!string.IsNullOrWhiteSpace(label))
                {
                    options.Add(new AIClarificationOption { Label = label, Value = ReadString(item, "value") });
                }
            }
        }

        return options;
    }

    private static JsonElement BuildSchema()
    {
        JsonObject schema = new()
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["question"] = new JsonObject { ["type"] = "string", [DescriptionKey] = "The disambiguating question." },
                ["options"] = new JsonObject
                {
                    ["type"] = "array",
                    [DescriptionKey] = "The discrete choices (2-5 recommended).",
                    ["items"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JsonObject
                        {
                            ["label"] = new JsonObject { ["type"] = "string", [DescriptionKey] = "Display label." },
                            ["value"] = new JsonObject { ["type"] = "string", [DescriptionKey] = "Optional value sent back when chosen." },
                        },
                        ["required"] = new JsonArray("label"),
                        ["additionalProperties"] = false,
                    },
                },
                ["allow_other"] = new JsonObject
                {
                    ["type"] = "boolean",
                    [DescriptionKey] = "Offer an 'Other (describe)' free-text affordance.",
                },
            },
            ["required"] = new JsonArray("question", "options"),
            ["additionalProperties"] = false,
        };

        return JsonSerializer.SerializeToElement(schema);
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool ReadBool(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out JsonElement value)
        && value.ValueKind is JsonValueKind.True or JsonValueKind.False
        && value.GetBoolean();
}
