using System.Diagnostics;

namespace Granit.AI.Anthropic.Diagnostics;

/// <summary>OpenTelemetry activity source for the Anthropic AI provider.</summary>
/// <remarks>
/// Spans follow the OpenTelemetry GenAI semantic conventions:
/// <c>gen_ai.system</c>, <c>gen_ai.request.model</c>, <c>gen_ai.usage.input_tokens</c>,
/// <c>gen_ai.usage.output_tokens</c>.
/// </remarks>
internal static class AIAnthropicActivitySource
{
    /// <summary>Activity source name.</summary>
    internal const string Name = "Granit.AI.Anthropic";

    /// <summary>Operation name for a non-streaming chat completion.</summary>
    internal const string ChatOperation = "anthropic.chat";

    /// <summary>Operation name for a streaming chat completion.</summary>
    internal const string ChatStreamOperation = "anthropic.chat.stream";

    /// <summary>OTel GenAI system identifier for Anthropic.</summary>
    internal const string SystemTagValue = "anthropic";

    /// <summary>Shared activity source instance.</summary>
    internal static readonly ActivitySource Source = new(Name);
}
