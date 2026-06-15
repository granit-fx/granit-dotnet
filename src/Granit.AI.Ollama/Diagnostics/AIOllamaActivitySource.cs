using System.Diagnostics;
using Granit.AI.Diagnostics;

namespace Granit.AI.Ollama.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.AI.Ollama distributed tracing.
/// </summary>
/// <remarks>
/// Spans follow the OpenTelemetry GenAI semantic conventions:
/// <c>gen_ai.system</c>, <c>gen_ai.request.model</c>, <c>gen_ai.usage.input_tokens</c>,
/// <c>gen_ai.usage.output_tokens</c>.
/// </remarks>
internal static class AIOllamaActivitySource
{
    /// <summary>The name of the Granit.AI.Ollama <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.AI.Ollama";

    /// <summary>Operation name for a non-streaming chat completion.</summary>
    internal const string ChatOperation = "ollama.chat";

    /// <summary>Operation name for a streaming chat completion.</summary>
    internal const string ChatStreamOperation = "ollama.chat.stream";

    /// <summary>OTel GenAI system identifier for Ollama.</summary>
    internal const string SystemTagValue = "ollama";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    /// <summary>Tracing identity supplied to <see cref="TracingChatClient"/>.</summary>
    internal static readonly AIChatTracingProfile TracingProfile =
        new(Source, SystemTagValue, ChatOperation, ChatStreamOperation);
}
