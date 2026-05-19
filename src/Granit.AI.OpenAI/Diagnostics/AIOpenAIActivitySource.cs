using System.Diagnostics;

namespace Granit.AI.OpenAI.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.AI.OpenAI distributed tracing.
/// </summary>
/// <remarks>
/// Spans follow the OpenTelemetry GenAI semantic conventions:
/// <c>gen_ai.system</c>, <c>gen_ai.request.model</c>, <c>gen_ai.usage.input_tokens</c>,
/// <c>gen_ai.usage.output_tokens</c>.
/// </remarks>
internal static class AIOpenAIActivitySource
{
    /// <summary>The name of the Granit.AI.OpenAI <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.AI.OpenAI";

    /// <summary>Operation name for a non-streaming chat completion.</summary>
    internal const string ChatOperation = "openai.chat";

    /// <summary>Operation name for a streaming chat completion.</summary>
    internal const string ChatStreamOperation = "openai.chat.stream";

    /// <summary>OTel GenAI system identifier for OpenAI.</summary>
    internal const string SystemTagValue = "openai";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);
}
